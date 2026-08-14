import json
import logging
import re

from app.core.config import Settings
from app.services.llm import LlmClient

logger = logging.getLogger(__name__)

CATEGORY_IDS = {"Hardware": 1, "Software": 2, "Network": 3, "Access": 4, "Other": 5}
PRIORITY_IDS = {"Low": 1, "Medium": 2, "High": 3, "Critical": 4}
DEFAULT_CATEGORY = "Other"
DEFAULT_PRIORITY = "Medium"

# Rule-based override layer (design rule #4): these MUST win over any model output.
CRITICAL_PATTERNS = [
    r"\bserver\s+(is\s+)?(down|offline|crashed|not\s+responding)",
    r"\boutage\b",
    r"\bsecurity\s+breach\b",
    r"\bdata\s+loss\b",
    r"\bransomware\b",
    r"\bvpn\s+(is\s+)?(down|offline).*\bentire\s+office",
    r"\bemail\s+(is\s+)?(down|offline)\s+for\s+everyone",
]

CATEGORY_RULES = [
    (
        "Network",
        [r"\bnetwork\b", r"\bwifi\b", r"\bwireless\b", r"\binternet\b", r"\brouter\b", r"\bvpn\b"],
    ),
    (
        "Access",
        [
            r"\baccess\b",
            r"\bpassword",
            r"\bpermission",
            r"\bcredentials",
            r"\blogin",
            r"\baccount\s+lock",
        ],
    ),
    (
        "Hardware",
        [r"\bhardware\b", r"\blaptop\b", r"\bmonitor\b", r"\bprinter\b", r"\bkeyboard\b", r"\bmouse\b"],
    ),
    (
        "Software",
        [
            r"\bsoftware\b",
            r"\bapplication\b",
            r"\bprogram\b",
            r"\bbug\b",
            r"\bcrashes?\b",
            r"\binstall",
            r"\bupdate\b",
        ],
    ),
]


class Classifier:
    """Hybrid category/priority classifier: rules first, LLM for the ambiguous middle."""

    def __init__(self, settings: Settings):
        self._settings = settings

    @staticmethod
    def _matches(text: str, patterns: list[str]) -> bool:
        return any(re.search(p, text) for p in patterns)

    async def classify(
        self,
        title: str,
        description: str,
        llm: LlmClient | None = None,
    ) -> dict:
        text = f"{title}\n{description}"
        lowered = text.lower()

        priority = "Critical" if self._matches(lowered, CRITICAL_PATTERNS) else None
        category = next(
            (
                name
                for name, patterns in CATEGORY_RULES
                if self._matches(lowered, patterns)
            ),
            None,
        )
        method = "rules" if (priority or category) else "llm"

        if priority is None or category is None:
            llm_result = None
            # Guard: only trust the LLM when the rules could not determine the
            # category. Rule-categorized tickets keep their rule/default priority
            # (rules stay authoritative per design rule #4; the LLM proved less
            # reliable at priorities than the rule/default baseline).
            if llm is not None and category is None:
                try:
                    llm_result = await self._llm_classify(llm, text)
                except Exception:  # noqa: BLE001
                    logger.warning("LLM classification failed, using defaults")
            if llm_result:
                if priority is None:
                    priority = llm_result.get("priority")
                if category is None:
                    category = llm_result.get("category")

        category = category if category in CATEGORY_IDS else DEFAULT_CATEGORY
        priority = priority if priority in PRIORITY_IDS else DEFAULT_PRIORITY
        return {
            "categoryId": CATEGORY_IDS[category],
            "category": category,
            "priorityId": PRIORITY_IDS[priority],
            "priority": priority,
            "method": method,
        }

    @staticmethod
    async def _llm_classify(llm: LlmClient, text: str) -> dict | None:
        prompt = (
            "Classify the following IT helpdesk issue into exactly one category and priority.\n"
            "Priority guidance:\n"
            "- High/Critical: core services, servers, network, or email are down or degraded for "
            "MANY users, or a security breach / data loss is suspected.\n"
            "- Low/Medium: a single user's device, account, or application issue that only affects "
            "that one person (broken laptop, password reset, slow WiFi, one app misbehaving).\n"
            "- A single broken device or account problem is never Critical.\n"
            "- When unsure, prefer Medium over High or Critical.\n"
            f"Ticket: {text[:2000]}\n\n"
            'Reply with only a JSON object, e.g. '
            '{"category": "Hardware|Software|Network|Access|Other", '
            '"priority": "Low|Medium|High|Critical"}'
        )
        response = await llm.complete(prompt, max_tokens=64, temperature=0.0)
        start = response.find("{")
        end = response.rfind("}")
        if start == -1 or end == -1 or end <= start:
            return None
        try:
            data = json.loads(response[start : end + 1])
        except json.JSONDecodeError:
            return None
        category = str(data.get("category", "")).strip()
        priority = str(data.get("priority", "")).strip()
        result: dict[str, str] = {}
        if category in CATEGORY_IDS:
            result["category"] = category
        if priority in PRIORITY_IDS:
            result["priority"] = priority
        return result or None
