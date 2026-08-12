import httpx


class TicketServiceError(Exception):
    def __init__(self, status_code: int, detail: str):
        super().__init__(detail)
        self.status_code = status_code
        self.detail = detail


def pick(obj: dict, camel: str, pascal: str, default: str = "") -> str:
    return obj.get(camel) or obj.get(pascal) or default


def render_ticket_thread(ticket: dict, comments: list[dict]) -> str:
    """Renders the ticket description + comments into a text block for LLM prompts."""
    ref = pick(ticket, "referenceNumber", "ReferenceNumber", "?")
    title = pick(ticket, "title", "Title", "(untitled)")
    description = pick(ticket, "description", "Description", "(no description)")
    status = pick(ticket, "statusName", "StatusName", "unknown")
    category = pick(ticket, "categoryName", "CategoryName", "uncategorized")
    priority = pick(ticket, "priorityName", "PriorityName", "medium")

    lines = [
        f"Ticket: {title} ({ref})",
        f"Category: {category} · Priority: {priority} · Status: {status}",
        "",
        "Description:",
        description,
    ]
    if comments:
        lines.append("")
        lines.append("Comments:")
        for i, comment in enumerate(comments, 1):
            content = pick(comment, "content", "Content", "")
            if content:
                lines.append(f"{i}. {content}")
    return "\n".join(lines)


async def fetch_ticket_thread(base_url: str, token: str, ticket_id: str) -> tuple[dict, list[dict]]:
    headers = {"Authorization": token}
    async with httpx.AsyncClient(timeout=30) as client:
        ticket_resp = await client.get(f"{base_url}/api/tickets/{ticket_id}", headers=headers)
        comments_resp = await client.get(
            f"{base_url}/api/tickets/{ticket_id}/comments", headers=headers
        )
    for resp in (ticket_resp, comments_resp):
        if resp.status_code != 200:
            raise TicketServiceError(resp.status_code, resp.text[:300])
    return ticket_resp.json(), comments_resp.json()
