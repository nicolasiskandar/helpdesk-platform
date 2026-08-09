def retryPush(String ref, int attempts = 4) {
    for (int i = 1; i <= attempts; i++) {
        try {
            sh "docker push ${ref}"
            return
        } catch (Exception e) {
            echo "docker push ${ref} attempt ${i}/${attempts} failed: ${e.getMessage()}"
            if (i < attempts) sleep(time: 10, unit: 'SECONDS')
        }
    }
    error "docker push ${ref} failed after ${attempts} attempts"
}

pipeline {
    agent { label 'built-in' }

    options {
        timestamps()
        ansiColor('xterm')
        disableConcurrentBuilds()
        buildDiscarder(logRotator(numToKeepStr: '20', artifactNumToKeepStr: '5'))
        timeout(time: 60, unit: 'MINUTES')
    }

    environment {
        // Controller's DOCKER_HOST points at the dind sidecar (tcp://docker:2375).
        // This is the daemon used for ephemeral build/test agents and image builds.
        DOCKER_HOST = 'tcp://docker:2375'
        GHCR_REGISTRY = 'ghcr.io/nicolasiskandar/helpdesk-platform'
        IMAGE_TAG = "${env.TAG_NAME ?: 'latest'}"
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        // ------------------------------------------------------------------
        // Build + tests. Backend uses dotnet/sdk:9.0 (tests target net9.0
        // while services target net8.0 — the 9.0 SDK builds both). Frontend
        // uses node:20-alpine with corepack/pnpm. ESLint is not installed in
        // the frontend, so CI only runs `pnpm build`.
        //
        // The backend services run SEQUENTIALLY (not in parallel) because they
        // all share the single `nuget-cache` volume mounted at
        // /root/.nuget/packages. NuGet's global-packages folder is not safe for
        // concurrent extraction: parallel `dotnet restore`s of the same
        // packages race on the temp .nnk/.tmp extraction files and corrupt the
        // cache ("Could not find file '.../xxx.nnk'"). Frontend still runs in
        // parallel with the whole backend since it uses its own caches.
        // ------------------------------------------------------------------
        stage('Build & Test') {
            failFast false
            parallel {
                stage('Backend') {
                    stages {
                        stage('Gateway build') {
                            agent {
                                docker {
                                    image 'mcr.microsoft.com/dotnet/sdk:9.0'
                                    reuseNode true
                                    args '-v nuget-cache:/root/.nuget/packages'
                                }
                            }
                            steps {
                                sh 'dotnet build services/gateway/src/Gateway/Gateway.csproj -c Release'
                            }
                        }
                        stage('Identity build + test') {
                            agent {
                                docker {
                                    image 'mcr.microsoft.com/dotnet/sdk:9.0'
                                    reuseNode true
                                    args '-v nuget-cache:/root/.nuget/packages'
                                }
                            }
                            steps {
                                sh 'dotnet build services/identity-service/IdentityService.sln -c Release'
                                sh 'dotnet test tests/IdentityService.Tests/'
                            }
                        }
                        stage('Ticket build + test') {
                            agent {
                                docker {
                                    image 'mcr.microsoft.com/dotnet/sdk:9.0'
                                    reuseNode true
                                    args '-v nuget-cache:/root/.nuget/packages'
                                }
                            }
                            steps {
                                sh 'dotnet build services/ticket-service/TicketService.sln -c Release'
                                sh 'dotnet test tests/TicketService.Tests/'
                            }
                        }
                        stage('Notification build + test') {
                            agent {
                                docker {
                                    image 'mcr.microsoft.com/dotnet/sdk:9.0'
                                    reuseNode true
                                    args '-v nuget-cache:/root/.nuget/packages'
                                }
                            }
                            steps {
                                sh 'dotnet build services/notification-service/NotificationService.sln -c Release'
                                sh 'dotnet test tests/NotificationService.Tests/'
                            }
                        }
                    }
                }
                stage('AI build + test') {
                    agent {
                        docker {
                            image 'python:3.12-slim'
                            reuseNode true
                        }
                    }
                    steps {
                        sh 'python -m venv /tmp/ai-venv'
                        sh '/tmp/ai-venv/bin/pip install --quiet -e "./services/ai-service[dev]"'
                        sh '/tmp/ai-venv/bin/ruff check services/ai-service/app services/ai-service/tests'
                        dir('services/ai-service') {
                            sh '/tmp/ai-venv/bin/python -m pytest -q'
                        }
                    }
                }
                stage('Frontend build') {
                    agent {
                        docker {
                            image 'node:20-alpine'
                            reuseNode true
                            args '-v pnpm-store:/pnpm/store -v corepack-cache:/root/.cache/node/corepack'
                        }
                    }
                    steps {
                        sh 'corepack enable && COREPACK_HOME=/root/.cache/node/corepack corepack prepare pnpm@9 --activate'
                        dir('frontend') {
                            sh 'pnpm install --frozen-lockfile --store-dir /pnpm/store'
                            sh 'pnpm build'
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // main + version tags: build the 6 images on the dind sidecar and push
        // them to GHCR (ghcr.io/nicolasiskandar/helpdesk-platform-*). All images
        // are built first, then pushed with retries — GHCR blob uploads can
        // stall on slow/flaky links, so a transient timeout must not abort the
        // whole stage.
        // ------------------------------------------------------------------
        stage('Build & Push Images') {
            when {
                anyOf {
                    branch 'main'
                    tag pattern: '^v?\\d+\\.\\d+\\.\\d+.*$'
                }
            }
            steps {
                script {
                    def tag = env.TAG_NAME ?: 'latest'
                    def images = [
                        [name: 'gateway',              context: 'services/gateway'],
                        [name: 'identity-service',     context: 'services/identity-service'],
                        [name: 'ticket-service',       context: 'services/ticket-service'],
                        [name: 'notification-service', context: 'services/notification-service'],
                        [name: 'ai-service',           context: 'services/ai-service'],
                        [name: 'frontend',             context: 'frontend'],
                    ]
                    withCredentials([usernamePassword(
                        credentialsId: 'ghcr',
                        usernameVariable: 'GHCR_USER',
                        passwordVariable: 'GHCR_PAT'
                    )]) {
                        sh 'echo "$GHCR_PAT" | docker login ghcr.io -u "$GHCR_USER" --password-stdin'

                        // 1) Build every image first so a push failure never
                        //    wastes the other builds.
                        images.each { img ->
                            def repo = "${GHCR_REGISTRY}-${img.name}"
                            sh "docker build -t ${repo}:${tag} ${img.context}"
                        }

                        // 2) Push each image with retries.
                        images.each { img ->
                            retryPush("${GHCR_REGISTRY}-${img.name}:${tag}")
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // main + version tags: deploy the stack on the HOST daemon. Uses the
        // mounted host socket (/var/run/docker.sock) — NOT dind. The repo is
        // synced to /opt/helpdesk-deploy (same path on host and controller, so
        // the host daemon resolves the compose ./infra/... bind sources),
        // .env is restored from the 'helpdesk-env' credential, then
        // remote-deploy.sh pulls the GHCR images (ghcr-deploy read token) and
        // brings the stack up with the image-only override (--no-build).
        // ------------------------------------------------------------------
        stage('Deploy') {
            when {
                anyOf {
                    branch 'main'
                    tag pattern: '^v?\\d+\\.\\d+\\.\\d+.*$'
                }
            }
            steps {
                withCredentials([string(
                    credentialsId: 'helpdesk-env',
                    variable: 'HELPDESK_ENV'
                )]) {
                    sh '''
                        set -eu
                        DEPLOY_DIR=/opt/helpdesk-deploy
                        # /opt/helpdesk-deploy is a bind mount (same path on
                        # host and controller) — the mount point itself can't be
                        # removed (EBUSY), so clear its contents instead.
                        mkdir -p "$DEPLOY_DIR"
                        find "$DEPLOY_DIR" -mindepth 1 -maxdepth 1 -exec rm -rf {} +
                        # Sync the COMMITTED tree only (git archive). The Jenkins
                        # workspace accumulates build cruft (node_modules,
                        # .pnpm-store, bin/obj, .next, @tmp dirs...) which a
                        # plain tar would drag in — and tarring it is slow enough
                        # to trip the durable-task watchdog. git archive also
                        # excludes gitignored files (.env, infra/certs), which
                        # are written/generated separately.
                        git archive --format=tar HEAD | tar -xf - -C "$DEPLOY_DIR"
                        # The 'helpdesk-env' credential holds a BASE64-ENCODED
                        # .env: Jenkins' secret-text field is single-line, so a
                        # raw multi-line .env gets its newlines stripped on
                        # paste. Decode it, then sanity-check the result.
                        printf '%s\\n' "$HELPDESK_ENV" | base64 -d > "$DEPLOY_DIR/.env"
                        for k in MSSQL_SA_PASSWORD MSSQL_DATABASE JWT_ISSUER JWT_AUDIENCE JWT_ACCESS_TOKEN_EXPIRY_MINUTES JWT_REFRESH_TOKEN_EXPIRY_DAYS; do
                            grep -q "^${k}=." "$DEPLOY_DIR/.env" || {
                                echo "ERROR: decoded .env is missing '${k}='. Set the 'helpdesk-env' credential to the output of: base64 -w 0 .env" >&2
                                exit 1
                            }
                        done
                        echo "Synced repo to $DEPLOY_DIR"
                    '''
                }
                withEnv(['DOCKER_HOST=unix:///var/run/docker.sock']) {
                    withCredentials([usernamePassword(
                        credentialsId: 'ghcr-deploy',
                        usernameVariable: 'GHCR_USER',
                        passwordVariable: 'GHCR_PAT'
                    )]) {
                        sh 'echo "$GHCR_PAT" | docker login ghcr.io -u "$GHCR_USER" --password-stdin'
                        sh "bash /opt/helpdesk-deploy/infra/jenkins/deploy/remote-deploy.sh \"${IMAGE_TAG}\""
                    }
                }
            }
        }
    }
}
