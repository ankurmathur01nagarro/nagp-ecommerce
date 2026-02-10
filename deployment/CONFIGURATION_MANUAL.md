# Observability Stack - Configuration Manual

## Overview

This manual documents the complete configuration of the observability stack including Grafana Alloy (OTel Collector replacement), Jaeger, Loki, Prometheus, and New Relic integration.

**Deployment Date:** February 8, 2026  
**Status:** ✅ Production Ready  
**Updated:** February 8, 2026

---

## Architecture

```
Applications (OTLP Instrumented)
            ↓
    Grafana Alloy (4317/gRPC, 4318/HTTP)
            ↓
    ┌───────┼───────┬──────────┐
    ↓       ↓       ↓          ↓
  Jaeger  Loki  Prometheus  New Relic
 (traces) (logs) (metrics)   (Cloud)
```

### Data Flow Pipelines

**Configured (per `helm-alloy-values.yaml`):**

| Pipeline | Alloy Receiver | Alloy Processors | Alloy Exporters |
|----------|----------------|-------------------|------------------|
| **Traces** | `otelcol.receiver.otlp` | `memory_limiter`, `batch "traces"` | `otelcol.exporter.otlp "jaeger"`, `otelcol.exporter.otlphttp "newrelic"` |
| **Logs** | `otelcol.receiver.otlp` | `memory_limiter`, `batch "logs"` | `otelcol.exporter.otlphttp "loki"`, `otelcol.exporter.otlphttp "newrelic"` |
| **Metrics** | `otelcol.receiver.otlp`, `prometheus.receive_http` → `otelcol.receiver.prometheus` | `memory_limiter`, `batch "metrics"` | `otelcol.exporter.otlphttp "newrelic"` |

> **Note:** Alloy uses a component-based config syntax (not YAML pipelines). Each signal
> type routes through its own `batch` processor instance. The config lives in
> `helm-alloy-values.yaml` under `alloy.configMap.content`.

> **Note:** After ArgoCD sync, verify the deployed config with:
> ```bash
> kubectl get configmap alloy -n observability -o jsonpath='{.data.config\.alloy}'
> ```

> **Prometheus bridge:** The old OTel `prometheusremotewrite` receiver is replaced by
> Alloy's native `prometheus.receive_http` → `otelcol.receiver.prometheus` bridge.
> The endpoint path changed from `/api/v1/write` to `/api/v1/metrics/write`.

---

## Component Configurations

### 1. Grafana Alloy (`helm-alloy-values.yaml`)

**Purpose:** Central telemetry pipeline that receives OTLP signals, bridges Prometheus
remote write, and exports to all backends. Replaces the OpenTelemetry Collector.

**Image:** `grafana/alloy:v1.13.0` (bundles OTel Collector v0.142.0 internally)
**Chart:** `alloy-1.6.0` (from `grafana.github.io/helm-charts`)
**Stability Level:** `public-preview` (required for `otelcol.receiver.prometheus` bridge)

#### Component-Based Config (Alloy syntax)

Alloy uses a component graph instead of YAML pipelines. The config is defined in
`helm-alloy-values.yaml` under `alloy.configMap.content`. Key component types:

| Component | Purpose |
|-----------|---------|
| `otelcol.receiver.otlp "default"` | Receives traces, logs, metrics via gRPC :4317 / HTTP :4318 |
| `prometheus.receive_http "default"` | Receives Prometheus remote write on :9090 `/api/v1/metrics/write` |
| `otelcol.receiver.prometheus "default"` | Bridges Prometheus metrics → OTel format |
| `otelcol.processor.memory_limiter "default"` | Prevents OOM (limit_mib: 400) |
| `otelcol.processor.batch "traces"/"logs"/"metrics"` | Per-signal batching (timeout: 5s, batch: 1024) |
| `otelcol.exporter.otlp "jaeger"` | Sends traces to Jaeger via gRPC |
| `otelcol.exporter.otlphttp "loki"` | Sends logs to Loki via OTLP HTTP |
| `otelcol.exporter.otlphttp "newrelic"` | Sends all signals to New Relic |

**Application Configuration to Send Data:**
```
gRPC:  http://alloy.observability:4317
HTTP:  http://alloy.observability:4318
```

#### Exporters

**Jaeger OTLP gRPC Exporter** (`otelcol.exporter.otlp "jaeger"`)
- **Endpoint:** `jaeger.observability.svc.cluster.local:4317`
- **Protocol:** OTLP gRPC
- **TLS:** `insecure = true` (Istio mTLS at mesh layer)

**Loki OTLP HTTP Exporter** (`otelcol.exporter.otlphttp "loki"`)
- **Endpoint:** `http://loki.observability.svc.cluster.local:3100/otlp`
- **Protocol:** OTLP HTTP
- **Note:** Loki 3.x native OTLP endpoint (exporter appends `/v1/logs` automatically)

**New Relic OTLP Exporter** (`otelcol.exporter.otlphttp "newrelic"`)
- **Endpoint:** `https://otlp.eu01.nr-data.net:4318`
- **Authentication:** API key from Kubernetes secret via `sys.env("NEW_RELIC_API_KEY")`
- **TLS:** Secure (default — required for external endpoints)
- **Queue:** `queue_size: 500`, `num_consumers: 4` (bounded, prevents memory growth)
- **Retry:** `max_elapsed_time: 120s` (drop data rather than OOM after 2 min)

#### Processors

**Memory Limiter** (`otelcol.processor.memory_limiter "default"`):
- Must be the first processor in every pipeline
- `limit_mib: 400`, `spike_limit_mib: 100`
- Works with `GOMEMLIMIT=400MiB` env var

**Batch Processors** (per-signal: `"traces"`, `"logs"`, `"metrics"`):
- `timeout: 5s`, `send_batch_size: 1024`
- Each routes to its own set of exporters

#### Prometheus Remote Write Bridge

Replaces the OTel Collector's `prometheusremotewrite` YAML receiver with Alloy's native bridge:
```
Prometheus → prometheus.receive_http (:9090) → otelcol.receiver.prometheus → memory_limiter → batch "metrics" → newrelic
```

**Key change:** The endpoint path changed from `/api/v1/write` to `/api/v1/metrics/write`.
Update `helm-prometheus-values.yaml` accordingly.

**v2 protocol no longer needed:** The old OTel `prometheusremotewrite` receiver required
`protobuf_message: "io.prometheus.write.v2.Request"`. Alloy's native `prometheus.receive_http`
uses standard Prometheus v1 protocol — no `protobuf_message` override required.

#### Environment Variables
```yaml
alloy:
  extraEnv:
    - name: NEW_RELIC_API_KEY
      valueFrom:
        secretKeyRef:
          name: newrelic-otel-secret
          key: api-key
    - name: GOMEMLIMIT
      value: "400MiB"
```

- **GOMEMLIMIT:** Sets the Go runtime's soft memory limit. Should match `memory_limiter.limit_mib`.

**Secret Creation:**
```bash
kubectl create secret generic newrelic-otel-secret \
  --from-literal=api-key=<YOUR_40_CHAR_API_KEY> \
  -n observability
```

**Resource Limits:**
- CPU: 100m (request) → 500m (limit)
- Memory: 128Mi (request) → 512Mi (limit)

---

### 2. Jaeger (`helm-jaeger-values.yaml`)

**Purpose:** Distributed tracing backend that stores and queries trace data.

#### Image Configuration
```yaml
jaeger:
  enabled: true
  image:
    repository: jaegertracing/jaeger    # Official Jaeger v2 image
    tag: "2.14.1"                       # Released v2 version (not v1 EOL)
    pullPolicy: IfNotPresent
```

**Version Information:**
- Current: v2.14.1 (Released)
- v1 reached EOL: December 31, 2025
- Migration guide: https://www.jaegertracing.io/docs/latest/migration/

> **CRITICAL:** Jaeger v2 uses an OTel Collector-based architecture. All v1-style env vars
> (`MEMORY_MAX_TRACES`, `COLLECTOR_OTLP_ENABLED`, `BADGER_*`) are **silently ignored**.
> Configuration must use the `userconfig` block with OTel Collector-style YAML.

#### v2 Configuration (userconfig)

Jaeger v2 is configured via a `userconfig` block that defines extensions, receivers,
exporters, and pipelines in OTel Collector format:

```yaml
userconfig:
  extensions:
    healthcheckv2:
      http:
        endpoint: "0.0.0.0:13133"
    jaeger_storage:
      backends:
        main_store:
          memory:
            max_traces: 5000
    jaeger_query:
      storage:
        traces: main_store
  receivers:
    otlp:
      protocols:
        grpc:
          endpoint: "0.0.0.0:4317"
        http:
          endpoint: "0.0.0.0:4318"
  exporters:
    jaeger_storage_exporter:
      trace_storage: main_store
  service:
    extensions: [healthcheckv2, jaeger_storage, jaeger_query]
    pipelines:
      traces:
        receivers: [otlp]
        exporters: [jaeger_storage_exporter]
```

#### Service Ports

| Port | Protocol | Purpose |
|------|----------|---------|
| 4317 | gRPC | OTLP receiver |
| 4318 | HTTP | OTLP receiver |
| 13133 | HTTP | Health check (v2 healthcheckv2 extension) |
| 14250 | gRPC | Jaeger/OTel receiver (all-in-one) |
| 16686 | HTTP | Query UI |
| 16685 | gRPC | Query gRPC |
| 6831 | UDP | Jaeger compact thrift |
| 6832 | UDP | Jaeger binary thrift |
| 9411 | HTTP | Zipkin receiver |

#### Health Checks

Jaeger v2 uses the `healthcheckv2` extension on **port 13133** (not 14269 from v1):

```yaml
# The Helm chart auto-configures probes based on userconfig.
# If customizing, use port 13133:
livenessProbe:
  httpGet:
    path: /status
    port: 13133
readinessProbe:
  httpGet:
    path: /status
    port: 13133
```

> **Important:** Port 14269 was the v1 admin server health endpoint. In v2, it does not exist.

#### Storage Configuration

**Current:** In-memory storage (max_traces: 5000)
- Suitable for: Development, testing, short-term tracing
- Limitation: Data lost on pod restart

**For Production:** Switch to Elasticsearch or BadgerDB backend:
```yaml
# Elasticsearch example (in userconfig.extensions.jaeger_storage.backends):
main_store:
  elasticsearch:
    server_urls: ["http://elasticsearch:9200"]
    index_prefix: "jaeger"

# BadgerDB example (persistent on-disk):
main_store:
  badger:
    directory_key: "/badger/key"
    directory_value: "/badger/data"
```

#### Security Context
```yaml
jaeger:
  podSecurityContext:
    runAsNonRoot: true
    runAsUser: 10001       # Matches Jaeger v2 Docker image UID
```

---

### 3. Loki (`helm-loki-values.yaml`)

**Purpose:** Log aggregation and storage system with native OTLP ingestion.

**Image:** `grafana/loki:3.6.4`  
**Chart:** `loki-6.52.0` (installed via `grafana/loki` standalone chart)  
**Pod Label:** `app.kubernetes.io/name=loki`  
**Deployment Mode:** SingleBinary

#### Deployment

Deployed as a StatefulSet via the standalone `grafana/loki` chart:
```bash
helm install loki grafana/loki -f helm-loki-values.yaml -n observability
```

> **Migration Note:** This replaces the earlier `grafana/loki-stack` chart (Loki 2.6.1)
> which did not support native OTLP log ingestion. The `loki` exporter was removed from
> otel-collector-contrib 0.145.0, requiring the upgrade to Loki 3.x with its native
> OTLP endpoint.

#### OTLP Configuration

Loki 3.x accepts OTLP logs natively at the `/otlp` endpoint. This requires:
```yaml
loki:
  limits_config:
    allow_structured_metadata: true   # Required for OTLP support
    retention_period: 168h            # 7-day retention policy
```

**OTLP Endpoint:** `http://loki.observability.svc.cluster.local:3100/otlp`  
The OTel Collector's `otlp_http/loki` exporter appends `/v1/logs` automatically.

#### Retention & Compactor

```yaml
loki:
  limits_config:
    retention_period: 168h            # 7 days
  compactor:
    retention_enabled: true
    working_directory: /var/loki/compactor
    delete_request_store: filesystem
```

- **retention_period:** Logs older than 168h (7 days) are marked for deletion
- **compactor:** Background process that enforces retention by deleting expired chunks
- **working_directory:** Must be `/var/loki/compactor` (default for Loki 3.x)

#### Storage Configuration

**Current (deployed):** TSDB with filesystem backend
```yaml
loki:
  schemaConfig:
    configs:
      - from: "2024-01-01"
        store: tsdb
        object_store: filesystem
        schema: v13
        index:
          prefix: index_
          period: 24h
  storage:
    type: filesystem
```

**Persistence:**
```yaml
singleBinary:
  persistence:
    enabled: true
    storageClass: local-path
    size: 10Gi
```

#### Service Endpoints

| Endpoint | Port | Purpose |
|----------|------|---------|
| `/otlp/v1/logs` | 3100 | OTLP log ingestion (native) |
| `/loki/api/v1/push` | 3100 | Loki-native log ingestion |
| `/loki/api/v1/query` | 3100 | Query logs (instant) |
| `/loki/api/v1/query_range` | 3100 | Query logs (range) |
| `/loki/api/v1/labels` | 3100 | List label names |
| `/loki/api/v1/label/{name}/values` | 3100 | List label values |

**Query Format:**
```bash
# Query by service name (OTLP-ingested logs use service_name label)
curl 'http://localhost:3100/loki/api/v1/query_range?query={service_name="test-app"}&limit=10'
```

#### Resource Limits
```yaml
singleBinary:
  resources:
    limits:
      cpu: 200m
      memory: 256Mi
    requests:
      cpu: 100m
      memory: 128Mi
```

---

### 4. Prometheus (`helm-prometheus-values.yaml`)

**Purpose:** Metrics collection and storage.

**Image:** `quay.io/prometheus/prometheus:v3.9.1`  
**Chart:** `prometheus-28.9.0` (installed via `prometheus-community/prometheus`)

#### Key Configuration

**Remote Write to Grafana Alloy:**
```yaml
server:
  extraArgs:
    enable-feature: metadata-wal-records
  remoteWrite:
    - url: http://alloy.observability:9090/api/v1/metrics/write
```

This sends Prometheus metrics via standard **Remote Write v1** protocol to Alloy's
`prometheus.receive_http` component, which bridges them into OTel format via
`otelcol.receiver.prometheus` and forwards to New Relic.

> **Note:** The `protobuf_message: "io.prometheus.write.v2.Request"` setting is no longer
> needed. The old OTel `prometheusremotewrite` receiver required v2; Alloy's native
> `prometheus.receive_http` uses standard v1 protocol.

**Disabled components (k8s metrics not in pipeline — Kiali handles Istio traffic):**
- `prometheus-node-exporter: enabled: false`
- `kube-state-metrics: enabled: false`
- `alertmanager: enabled: false`
- `prometheus-pushgateway: enabled: false`

#### Scrape Configuration

**Default targets (auto-discovered):**
- kube-state-metrics (pod/node metrics)
- node-exporter (host metrics)
- kubelet

**Custom targets:** Add to `prometheus.yml` in values:
```yaml
scrape_configs:
  - job_name: 'my-app'
    static_configs:
      - targets: ['my-app:8080']
```

#### Service Endpoints

| Port | Purpose |
|------|---------|
| 80 (ClusterIP) | Metrics API & UI (maps to container port 9090) |

**Query Examples:**
```bash
# All metrics
curl http://localhost:9090/api/v1/query?query=up

# Pod restart count
curl http://localhost:9090/api/v1/query?query=kube_pod_container_status_restarts_total

# Memory usage
curl http://localhost:9090/api/v1/query?query=container_memory_usage_bytes
```

#### Retention Policy

Explicit retention configured:
```yaml
server:
  retention: "15d"           # Time-based retention
  retentionSize: "8GB"       # Size-based retention limit
```

When both are set, whichever limit is reached first triggers cleanup.

#### Resource Limits

```yaml
server:
  resources:
    requests:
      cpu: 250m
      memory: 512Mi
    limits:
      cpu: "1"
      memory: 2Gi
```

#### Remote Write Path

Prometheus → (Remote Write v1) → Alloy (`prometheus.receive_http` on port 9090 `/api/v1/metrics/write`) → `otelcol.receiver.prometheus` (Prom→OTel bridge) → `otelcol.processor.memory_limiter` → `otelcol.processor.batch "metrics"` → New Relic (`otelcol.exporter.otlphttp "newrelic"`).

---

### 5. Grafana (`helm-grafana-values.yaml`)

**Purpose:** Unified dashboarding UI with pre-configured datasources for Jaeger, Loki, and Prometheus.

**Chart:** `grafana/grafana` (migrating to `grafana-community/grafana` after Jan 30, 2026)

#### Authentication

Credentials are stored in a Kubernetes secret (not in values file):
```yaml
admin:
  existingSecret: grafana-admin-secret
```

**Create the secret before deploying:**
```bash
kubectl create secret generic grafana-admin-secret \
  --from-literal=admin-user=admin \
  --from-literal=admin-password=changeme \
  -n observability
```

#### Persistence

```yaml
persistence:
  enabled: true
  storageClassName: local-path
  size: 1Gi
```

Persists dashboards, preferences, and annotations across pod restarts.

#### Pre-configured Datasources

```yaml
datasources:
  datasources.yaml:
    datasources:
      - name: Jaeger
        type: jaeger
        url: http://jaeger.observability.svc.cluster.local:16686
      - name: Loki
        type: loki
        url: http://loki.observability.svc.cluster.local:3100
      - name: Prometheus
        type: prometheus
        url: http://prometheus-server.observability.svc.cluster.local:80
        isDefault: true
```

#### Access

```bash
kubectl port-forward -n observability svc/grafana 3000:3000
# Open http://localhost:3000 — credentials from grafana-admin-secret
```

---

### 6. Istio Telemetry (`istio-resources.k8s.yaml`)

**Purpose:** Configure Istio service mesh to emit traces to Grafana Alloy.

```yaml
apiVersion: telemetry.istio.io/v1
kind: Telemetry
metadata:
  name: mesh-default
  namespace: istio-system
spec:
  tracing:
    - providers:
        - name: otel-tracing
      randomSamplingPercentage: 5     # 5% of requests sampled
```

> **Sampling:** Reduced from 100% to 5% for production. Higher values generate excessive
> trace volume. For critical traces, use tail-based sampling in the OTel Collector
> (requires the `tail_sampling` processor).
> In Alloy, this would be `otelcol.processor.tail_sampling`.

---

## New Relic Integration

### API Key Setup

1. **Get API Key:**
   - Login to https://one.newrelic.com
   - Account Settings → API Keys
   - Create/copy License Key

2. **Create Kubernetes Secret:**
   ```bash
   kubectl create secret generic newrelic-otel-secret \
     --from-literal=api-key=<YOUR_API_KEY> \
     -n observability
   ```

3. **Verify Secret:**
   ```bash
   kubectl get secret newrelic-otel-secret -n observability -o jsonpath='{.data.api-key}' | base64 -d
   ```

### Data Flow to New Relic

| Signal | Via OTel Exporter | Endpoint |
|--------|-------------------|----------|
| Traces | otlp_http/newrelic | otlp.eu01.nr-data.net:4318 |
| Logs | otlp_http/newrelic | otlp.eu01.nr-data.net:4318 |
| Metrics | otlp_http/newrelic | otlp.eu01.nr-data.net:4318 |

### Accessing Data in New Relic

1. **Traces:** One → All entities → Traces → Search
2. **Logs:** Logs → All logs
3. **Metrics:** Metrics explorer → OTLP data

---

## Deployment Instructions

### GitOps Deployment (ArgoCD App-of-Apps)

All observability components are managed via ArgoCD using the **App-of-Apps** pattern. The bootstrap script (`create-cluster.bat`) handles only infrastructure prerequisites; ArgoCD syncs everything else from Git.

#### Sync Wave Order

| Wave | Application | Namespace |
|------|-------------|-----------|
| 0 | istio-telemetry | istio-system |
| 1 | loki, jaeger, prometheus | observability |
| 2 | alloy (Grafana Alloy) | observability |
| 3 | grafana | observability |
| 5 | nagp-ecom-ui, nagp-ecom-api | nagp-ecom |

#### ArgoCD Configuration (`helm-argocd-values.yaml`)

The ArgoCD Helm install uses a custom values file that restores the **Application health check** (removed in ArgoCD v1.8). This is required for sync waves to work — without it, ArgoCD won't wait for child apps to become Healthy before proceeding to the next wave.

#### AppProjects

| Project | Scope | sourceRepos |
|---------|-------|-------------|
| `nagp-ecom` | Microservice apps → `nagp-ecom` namespace | Git repo only |
| `observability` | Observability stack → `observability` + `istio-system` namespaces | Wildcard `*` |

#### Multi-Source Applications

Helm chart apps use `spec.sources` (plural) with two sources:
1. **Helm chart** from the external repo (e.g., `https://grafana.github.io/helm-charts`)
2. **Git repo** referenced via `ref: values` — provides values files from `deployment/helm-*-values.yaml`

Example: ArgoCD resolves `$values/deployment/helm-alloy-values.yaml` to the Git repo root.

### Pre-requisites (kept in `create-cluster.bat`)

The bootstrap script still handles items that must exist before ArgoCD:

```bash
# 1. Create observability namespace
kubectl create namespace observability

# 2. Create secrets
kubectl create secret generic newrelic-otel-secret \
  --from-literal=api-key=$NEW_RELIC_API_KEY \
  -n observability

kubectl create secret generic grafana-admin-secret \
  --from-literal=admin-user=admin \
  --from-literal=admin-password=changeme \
  -n observability

# 3. Install ArgoCD with health check values
helm install argocd argo/argo-cd -n argocd \
  -f deployment/helm-argocd-values.yaml
```

### Configuration Updates

Since all observability components are ArgoCD-managed, updates are done via Git:

```bash
# Edit the values file (e.g., helm-alloy-values.yaml)
# Commit and push to Git
git add . && git commit -m "update alloy config" && git push

# ArgoCD auto-syncs the changes (selfHeal + prune enabled)
# To force immediate sync:
argocd app sync alloy
```

### Manual Helm Operations (emergency only)

If ArgoCD is down or for debugging, you can still use Helm directly:

```bash
# Verify deployed Alloy config
kubectl get configmap alloy -n observability -o jsonpath='{.data.config\.alloy}'
```

### Automated Setup (`create-cluster.bat`)

The `create-cluster.bat` script handles cluster bootstrapping prerequisites:
- k3d cluster creation (1 server + 2 agents)
- Istio ambient profile installation
- Namespace + secret creation for observability
- ArgoCD installation with health check values file
- ArgoCD App-of-Apps deployment (triggers GitOps sync of all components)

**Observability components** are no longer installed by the script — they are managed entirely by ArgoCD via the App-of-Apps pattern.

**API Key:** The script reads `%NEW_RELIC_API_KEY%` from the environment. Set it before running:
```bat
set NEW_RELIC_API_KEY=your-40-char-license-key
create-cluster.bat
```
The script exits with an error if the env var is not set.

**Grafana chart:** Uses `grafana-community/grafana` repo (migrated from `grafana/grafana` after Jan 30, 2026).  
**Loki chart:** Uses official `grafana/loki` repo (not `grafana-community`).

---

## Verification Procedures

### 1. Pod Status

```bash
# Check all pods
kubectl get pods -n observability

# Expected output:
# jaeger-xxxx                 1/1 Running
# loki-0                      2/2 Running    (loki + sidecar)
# alloy-xxxx                  1/1 Running
# prometheus-server-xxxx      1/1 Running    (server only, kube-state-metrics disabled)
```

### 2. Service Connectivity

```bash
# Check service IPs
kubectl get svc -n observability

# Test connectivity within cluster
kubectl run -it --rm debug --image=busybox -n observability -- sh
# Inside pod: telnet jaeger.observability 14250
```

### 3. Logs Verification

```bash
# Alloy logs
kubectl logs -n observability -l app.kubernetes.io/name=alloy

# Jaeger logs (should show "Everything is ready")
kubectl logs -n observability -l app.kubernetes.io/name=jaeger

# Loki logs
kubectl logs -n observability -l app.kubernetes.io/name=loki

# Prometheus server logs
kubectl logs -n observability -l app.kubernetes.io/component=server,app.kubernetes.io/name=prometheus
```

### 4. Data Flow Test

```bash
# 1. Port-forward to Jaeger
kubectl port-forward svc/jaeger -n observability 16686:16686 &

# 2. Open http://localhost:16686

# 3. If no data yet, manually send test data or:
#    - Ensure your app is instrumented with OTel SDK
#    - Configure app to send to http://alloy.observability:4317
#    - Generate some activity in your app

# 4. Verify in New Relic
#    - Go to https://one.newrelic.com
#    - Check Traces, Logs, Metrics
```

---

## Troubleshooting

### Issue: Jaeger pod stuck at 0/1

**Cause:** Health probe misconfiguration (v2 uses port 13133, not v1's 14269)

**Solution:**
```bash
# Check probe configuration
kubectl get deployment jaeger -n observability -o yaml | grep -A5 "livenessProbe"

# Should show port: 13133 (healthcheckv2 extension)
# If using v1 port 14269, update helm-jaeger-values.yaml to use v2 userconfig format
# and ensure healthcheckv2 extension is configured on port 13133
```

### Issue: Alloy not exporting to New Relic

**Cause:** Invalid API key or network issue

**Solution:**
```bash
# Check secret
kubectl get secret newrelic-otel-secret -n observability --show-literals

# Check logs for errors
kubectl logs -n observability -l app.kubernetes.io/name=alloy | grep -i "newrelic\|error"

# Recreate secret if needed
kubectl delete secret newrelic-otel-secret -n observability
kubectl create secret generic newrelic-otel-secret \
  --from-literal=api-key=<YOUR_API_KEY> \
  -n observability

# Restart Alloy
kubectl rollout restart deployment/alloy -n observability
```

### Issue: Loki disk full

**Cause:** Too much log retention

**Solution:**
```bash
# Check disk usage
kubectl exec -n observability loki-0 -- du -sh /data/loki

# Option 1: Clear old WAL checkpoints
kubectl exec -n observability loki-0 -- find /data/loki -mtime +7 -delete

# Option 2: Increase PVC (if storageClass supports expansion):
kubectl edit pvc storage-loki-0 -n observability
# Change storage: 20Gi
```

### Issue: Prometheus remote write not reaching Alloy

**Cause:** Alloy's `prometheus.receive_http` listens on `/api/v1/metrics/write` (port 9090),
not the old OTel path `/api/v1/write`.

**Solution:**
```yaml
# In helm-prometheus-values.yaml, use the Alloy endpoint:
server:
  remoteWrite:
    - url: http://alloy.observability:9090/api/v1/metrics/write
```
Then commit and push; ArgoCD handles the sync.

### Issue: Loki rejects logs with "timestamp too old"

**Cause:** Log entries sent with timestamps older than Loki's retention window are rejected
with HTTP 400: `"entry has timestamp too old"`.

**Solution:** Ensure log producers use current timestamps. For test logs via curl,
generate the timestamp dynamically:
```bash
# PowerShell:
$currentNano = [string]([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()) + "000000"
# Use $currentNano in the timeUnixNano field
```

### Issue: High memory usage

**Cause:** Large batch sizes, high throughput, or missing memory protection

**Solution:**
The `memory_limiter` processor is already configured as the first processor in all pipelines.
It will refuse data when memory exceeds `limit_mib` (400MiB). If you still see OOM:

In `helm-alloy-values.yaml`, reduce batch sizes in the `alloy.configMap.content` block:
```
otelcol.processor.batch "traces" {
  timeout         = "5s"
  send_batch_size = 256      // was 1024
}
```

Also ensure `GOMEMLIMIT` is set to match `limit_mib`.

---

## Performance Tuning

### OTel/Alloy Pipeline

In `helm-alloy-values.yaml`, adjust batch sizes in the `alloy.configMap.content` block:
```
// For high throughput:
otelcol.processor.batch "traces" {
  timeout         = "1s"
  send_batch_size = 2048
}

// In alloy.resources:
resources:
  limits:
    cpu: 1000m          # Increase from 500m
    memory: 1024Mi      # Increase from 512Mi
```

### Jaeger

```yaml
# For high volume traces:
resources:
  limits:
    cpu: 2000m
    memory: 2048Mi
```

### Loki

```yaml
# For high log volume:
persistence:
  size: 50Gi          # Increase if needed
  
resources:
  limits:
    cpu: 500m
    memory: 512Mi
```

---

## Backup and Recovery

### Backup Persistent Data

```bash
# Backup Loki data
kubectl get pvc loki -n observability -o yaml > loki-pvc.yaml
kubectl exec -n observability deploy/loki -- tar czf - /loki | gzip > loki-data-backup.tar.gz

# Backup all manifests
kubectl get all -n observability -o yaml > observability-backup.yaml
```

### Restore

```bash
# Restore namespace
kubectl apply -f observability-backup.yaml

# Restore Loki data
tar xzf loki-data-backup.tar.gz -C /
```

---

## Monitoring & Alerts

### Key Metrics to Monitor

```
# OTel Collector
- otelcol_receiver_accepted_spans
- otelcol_exporter_sent_spans
- otelcol_exporter_send_failed_spans

# Jaeger
- jaeger_spans_received_total
- jaeger_storage_write_latency

# Loki
- loki_ingester_received_lines
- loki_ingester_sent_chunks

# Prometheus
- up (target scrape status)
- scrape_duration_seconds
```

### New Relic NRQL Queries

```

# Count received traces
SELECT count(*) FROM Span WHERE instrumentation.provider='opentelemetry'

# Log volume over time
SELECT count(*) FROM Log WHERE service.name='your-service' TIMESERIES

# P99 latency
SELECT percentile(duration, 99) FROM Span WHERE instrumentation.provider='opentelemetry'
```

---

## Security Considerations

### Network Policies

```yaml
# Allow OTel to Jaeger
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: allow-otel-to-backends
spec:
  podSelector:
    matchLabels:
      app: jaeger
  policyTypes:
  - Ingress
  ingress:
  - from:
    - podSelector:
        matchLabels:
          app: alloy
    ports:
    - protocol: TCP
      port: 4317
```

### Secret Management

- API keys stored in Kubernetes secrets
- Use external secret providers (HashiCorp Vault) for production
- Rotate keys periodically
- Use RBAC to limit secret access

### TLS Configuration

- **Alloy → New Relic:** Secure TLS (default, no `insecure: true`)
- **Alloy → Internal backends:** `insecure = true` (Istio mTLS handles encryption at mesh layer)
- **Jaeger:** Internal only, HTTP endpoints (Istio mTLS provides transport encryption)
- **Loki:** Internal only, HTTP endpoints (Istio mTLS provides transport encryption)

> **Note:** For non-Istio deployments, configure explicit TLS certificates for all internal connections.

---

## Version Information

| Component | Image | Chart Version | Status |
|-----------|-------|---------------|--------|
| Jaeger | `jaegertracing/jaeger:2.14.1` | jaeger-4.4.6 | ✅ Supported |
| Loki | `grafana/loki:3.6.4` | loki-6.52.0 | ✅ Supported (official `grafana` repo) |
| Grafana Alloy | `grafana/alloy:v1.13.0` | alloy-1.6.0 | ✅ Supported (replaces OTel Collector) |
| Prometheus | `quay.io/prometheus/prometheus:v3.9.1` | prometheus-28.9.0 | ✅ Supported |
| Grafana | `grafana/grafana` | grafana | ✅ Supported (`grafana-community` repo) |
| New Relic | Cloud (EU endpoint) | N/A | ✅ Connected |

---

## Support & Documentation

- **Jaeger:** https://www.jaegertracing.io/docs/
- **Loki:** https://grafana.com/docs/loki/
- **Grafana Alloy:** https://grafana.com/docs/alloy/latest/
- **OpenTelemetry:** https://opentelemetry.io/docs/
- **Prometheus:** https://prometheus.io/docs/
- **New Relic:** https://docs.newrelic.com/

---

**Document Version:** 2.0 — Production Hardening Update  
**Last Updated:** February 8, 2026  
**Maintaining Team:** Observability Engineering
