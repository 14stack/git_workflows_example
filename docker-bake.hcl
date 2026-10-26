# Image build definitions shared by `just images-build`, `just security-images` (scan) and
# `just images-publish` (release). Compose builds the same Dockerfiles for local runs.
#
#   docker buildx bake                       # build all images tagged :dev
#   VERSION=1.4.0 docker buildx bake --push  # what the release workflow does

variable "REGISTRY" {
  default = "ghcr.io/14stack/git_workflows_example"
}

variable "VERSION" {
  default = "dev"
}

variable "SOURCE_URL" {
  default = "https://github.com/14stack/git_workflows_example"
}

variable "REVISION" {
  default = ""
}

# Releases get X.Y.Z, X.Y and latest; anything else only its own tag.
function "tags" {
  params = [name]
  result = can(regex("^[0-9]+\\.[0-9]+\\.[0-9]+$", VERSION)) ? [
    "${REGISTRY}/${name}:${VERSION}",
    "${REGISTRY}/${name}:${regex_replace(VERSION, "^([0-9]+\\.[0-9]+)\\..*$", "$1")}",
    "${REGISTRY}/${name}:latest",
  ] : ["${REGISTRY}/${name}:${VERSION}"]
}

group "default" {
  targets = ["jobs-api", "jobs-api-migrator", "ml-worker", "web"]
}

target "_common" {
  args = {
    APP_VERSION = VERSION
  }
  labels = {
    "org.opencontainers.image.source"   = SOURCE_URL
    "org.opencontainers.image.version"  = VERSION
    "org.opencontainers.image.revision" = REVISION
    "org.opencontainers.image.vendor"   = "14stack"
  }
}

target "jobs-api" {
  inherits = ["_common"]
  context  = "apps/jobs-api"
  target   = "runtime"
  tags     = tags("jobs-api")
  labels = {
    "org.opencontainers.image.title"       = "jobs-api"
    "org.opencontainers.image.description" = "ASP.NET Core API that stores ML jobs and dispatches them via RabbitMQ"
  }
}

target "jobs-api-migrator" {
  inherits = ["_common"]
  context  = "apps/jobs-api"
  target   = "migrator"
  tags     = tags("jobs-api-migrator")
  labels = {
    "org.opencontainers.image.title"       = "jobs-api-migrator"
    "org.opencontainers.image.description" = "EF Core migration bundle for the jobs database"
  }
}

target "ml-worker" {
  inherits = ["_common"]
  context  = "apps/ml-worker"
  tags     = tags("ml-worker")
  labels = {
    "org.opencontainers.image.title"       = "ml-worker"
    "org.opencontainers.image.description" = "Python worker that trains scikit-learn models for queued jobs"
  }
}

target "web" {
  inherits = ["_common"]
  context  = "apps/web"
  tags     = tags("web")
  labels = {
    "org.opencontainers.image.title"       = "web"
    "org.opencontainers.image.description" = "Angular UI served by unprivileged nginx"
  }
}
