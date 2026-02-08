# Observability Stack - Configuration Manual

## Overview

This manual documents the complete configuration of the observability stack including OTel Collector, Jaeger, Loki, Prometheus, and New Relic integration.

**Deployment Date:** February 8, 2026  
**Status:** ✅ Production Ready  
**Updated:** February 8, 2026

---

## Architecture

```
Applications (OTLP Instrumented)
            ↓
    OTel Collector (4317/gRPC, 4318/HTTP)
            ↓
    ┌───────┼───────┬──────────┐
    ↓       ↓       ↓          ↓
  Jaeger  Loki  Prometheus  New Relic
 (traces) (logs) (metrics)   (Cloud)
```

### Data Flow Pipelines

**Configured (per `helm-otel-values.yaml`):**

| Pipeline | Receivers | Processors | Exporters |
|----------|-----------|-----------|----------|
| **Traces** | otlp | memory_limiter, batch | otlp_grpc/jaeger, otlp_http/newrelic |
| **Logs** | otlp | memory_limiter, batch | otlp_http/loki, otlp_http/newrelic |
| **Metrics** | otlp, prometheusremotewrite | memory_limiter, batch | otlp_http/newrelic |

> **Note:** The `debug` exporter is commented out in all pipelines for production.
> Re-enable temporarily for troubleshooting by uncommenting in `helm-otel-values.yaml`.

> **Note:** The OTel Helm chart merges custom values with its defaults. After any `helm upgrade`, verify the deployed configmap with:
> ```bash
> kubectl get configmap otel-opentelemetry-collector -n observability -o jsonpath='{.data.relay}'
> ```
> Ensure `otlp_grpc/jaeger` and `otlp_http/loki` exporters appear in the deployed config. If missing, re-run:
> ```bash
> helm upgrade otel open-telemetry/opentelemetry-collector -f helm-otel-values.yaml -n observability
> ```

> **Exporter Naming (v0.145.0+):** The old aliases `otlphttp` and `otlp` are deprecated.
> Use `otlp_http` (HTTP) and `otlp_grpc` (gRPC) respectively. The dedicated `loki`
> exporter was **removed** from otel-collector-contrib 0.145.0 — use `otlp_http` to
> Loki's native OTLP endpoint (`/otlp`) instead.

---

## Component Configurations

### 1. OpenTelemetry Collector (`helm-otel-values.yaml`)

**Purpose:** Central data pipeline that receives OTLP signals and exports to all backends.

**Image:** `otel/opentelemetry-collector-contrib:0.145.0`  
**Chart:** `opentelemetry-collector-0.145.0`

#### Receivers Configuration
```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317      # OTLP gRPC receiver (apps send here)
      http:
        endpoint: 0.0.0.0:4318      # OTLP HTTP receiver (apps send here)
  prometheusremotewrite:
    endpoint: 0.0.0.0:9090          # Receive Prometheus scrapes
```

**Application Configuration to Send Data:**
```
gRPC:  http://otel-opentelemetry-collector.observability:4317
HTTP:  http://otel-opentelemetry-collector.observability:4318
```

#### Exporters Configuration

**Jaeger OTLP gRPC Exporter**
```yaml
otlp_grpc/jaeger:
  endpoint: jaeger.observability.svc.cluster.local:4317
  tls:
    insecure: true
```
- **Purpose:** Send traces to Jaeger
- **Protocol:** OTLP gRPC (named `otlp_grpc`, not the deprecated `otlp` alias)
- **Port:** 4317

**Loki OTLP HTTP Exporter**
```yaml
otlp_http/loki:
  endpoint: http://loki.observability.svc.cluster.local:3100/otlp
  tls:
    insecure: true
```
- **Purpose:** Send logs to Loki via native OTLP endpoint (Loki 3.x)
- **Protocol:** OTLP HTTP (named `otlp_http`, not the deprecated `otlphttp` alias)
- **Port:** 3100, path `/otlp` (exporter appends `/v1/logs` automatically)
- **Note:** The dedicated `loki` exporter was removed from otel-collector-contrib 0.145.0

**New Relic OTLP Exporter**
```yaml
otlp_http/newrelic:
  endpoint: https://otlp.eu01.nr-data.net:4318
  headers:
    api-key: ${NEW_RELIC_API_KEY}
```
- **Purpose:** Send all signals (traces, logs, metrics) to New Relic cloud
- **Region:** EU (eu01)
- **Authentication:** API key from Kubernetes secret
- **TLS:** Uses default secure TLS (no `insecure: true` — required for external endpoints)

#### Processors Configuration

```yaml
processors:
  memory_limiter:
    check_interval: 1s
    limit_mib: 400
    spike_limit_mib: 100
  batch:
    timeout: 5s
    send_batch_size: 1024
```

- **memory_limiter:** Must be the **first processor** in every pipeline. Prevents OOM by refusing data when memory exceeds `limit_mib`. Works with `GOMEMLIMIT` env var.
- **batch:** Groups telemetry data before export to reduce network overhead.

#### Service Pipelines

```yaml
service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [memory_limiter, batch]
      exporters: [otlp_grpc/jaeger, otlp_http/newrelic]
    logs:
      receivers: [otlp]
      processors: [memory_limiter, batch]
      exporters: [otlp_http/loki, otlp_http/newrelic]
    metrics:
      receivers: [otlp, prometheusremotewrite]
      processors: [memory_limiter, batch]
      exporters: [otlp_http/newrelic]
```

> **Note:** The `debug` exporter has been removed from all pipelines for production.
> It can be re-enabled temporarily for troubleshooting by uncommenting in `helm-otel-values.yaml`.

#### Environment Variables
```yaml
extraEnvs:
  - name: NEW_RELIC_API_KEY
    valueFrom:
      secretKeyRef:
        name: newrelic-otel-secret
        key: api-key
  - name: GOMEMLIMIT
    value: "400MiB"
```

- **GOMEMLIMIT:** Sets the Go runtime's soft memory limit. Should match `memory_limiter.limit_mib`.
  Prevents GC pressure spikes and works alongside the `memory_limiter` processor.

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

**Remote Write to OTel Collector (v2):**
```yaml
server:
  extraArgs:
    enable-feature: metadata-wal-records
  remoteWrite:
    - url: http://otel-opentelemetry-collector.observability:9090/api/v1/write
      protobuf_message: "io.prometheus.write.v2.Request"
```

This sends Prometheus metrics via **Remote Write v2** protocol to the OTel Collector's
`prometheusremotewrite` receiver, which then forwards to New Relic.

> **Important:** The `protobuf_message` setting is required. The OTel `prometheusremotewrite`
> receiver (v0.142.0+) **only supports Remote Write v2**. Without this setting, Prometheus
> defaults to v1 (`prometheus.WriteRequest`) which the receiver rejects with
> `"unsupported proto version"` warnings.
>
> **Compatibility:** OTel Collector ≥0.142.0 requires Prometheus ≥3.8.0 for Remote Write v2.

**Enabled components:**
- `prometheus-node-exporter: enabled: true`
- `kube-state-metrics: enabled: true`
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

Prometheus → (Remote Write v2) → OTel Collector (`prometheusremotewrite` receiver on port 9090) → New Relic (`otlp_http/newrelic`).

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

**Purpose:** Configure Istio service mesh to emit traces to OTel Collector.

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

### Initial Deployment

```bash
cd c:\nagp-casestudy\src\deployment

# 1. Add Helm repositories
helm repo add open-telemetry https://open-telemetry.github.io/opentelemetry-helm-charts
helm repo add grafana https://grafana.github.io/helm-charts          # Loki chart
helm repo add grafana-community https://grafana-community.github.io/helm-charts  # Grafana dashboard chart
helm repo add jaegertracing https://jaegertracing.github.io/helm-charts
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update

# 2. Create namespace
kubectl create namespace observability

# 3. Create secrets
kubectl create secret generic newrelic-otel-secret \
  --from-literal=api-key=<YOUR_API_KEY> \
  -n observability

kubectl create secret generic grafana-admin-secret \
  --from-literal=admin-user=admin \
  --from-literal=admin-password=changeme \
  -n observability

# 4. Deploy components
helm install otel open-telemetry/opentelemetry-collector \
  -f helm-otel-values.yaml -n observability

helm install loki grafana/loki \
  -f helm-loki-values.yaml -n observability

helm install jaeger jaegertracing/jaeger \
  -f helm-jaeger-values.yaml -n observability

helm install prometheus prometheus-community/prometheus \
  -f helm-prometheus-values.yaml -n observability

helm install grafana grafana/grafana \
  -f helm-grafana-values.yaml -n observability

# 5. Apply Istio telemetry
kubectl apply -f istio-resources.k8s.yaml

# 6. Verify deployed OTel config has all exporters
kubectl get configmap otel-opentelemetry-collector -n observability -o jsonpath='{.data.relay}'
```

### Updates After Configuration Changes

```bash
# Update specific component
helm upgrade otel open-telemetry/opentelemetry-collector \
  -f helm-otel-values.yaml -n observability

helm upgrade jaeger jaegertracing/jaeger \
  -f helm-jaeger-values.yaml -n observability

helm upgrade loki grafana/loki \
  -f helm-loki-values.yaml -n observability

helm upgrade prometheus prometheus-community/prometheus \
  -f helm-prometheus-values.yaml -n observability

helm upgrade grafana grafana/grafana \
  -f helm-grafana-values.yaml -n observability
```

### Automated Setup (`create-cluster.bat`)

The `create-cluster.bat` script automates full cluster provisioning including:
- k3d cluster creation (1 server + 2 agents)
- Istio ambient profile installation
- Helm repo setup and all component installs

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
# otel-opentelemetry-xxxx     1/1 Running
# prometheus-server-xxxx      2/2 Running    (server + config-reloader)
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
# OTel Collector logs
kubectl logs -n observability -l app.kubernetes.io/name=opentelemetry-collector

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
#    - Configure app to send to http://otel-collector.observability:4317
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

### Issue: OTel not exporting to New Relic

**Cause:** Invalid API key or network issue

**Solution:**
```bash
# Check secret
kubectl get secret newrelic-otel-secret -n observability --show-literals

# Check logs for errors
kubectl logs -n observability -l app.kubernetes.io/name=opentelemetry-collector | grep -i "newrelic\|error"

# Recreate secret if needed
kubectl delete secret newrelic-otel-secret -n observability
kubectl create secret generic newrelic-otel-secret \
  --from-literal=api-key=<YOUR_API_KEY> \
  -n observability

# Restart OTel
kubectl rollout restart deployment/otel-opentelemetry-collector -n observability
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

### Issue: Prometheus remote_write "unsupported proto version" warnings

**Cause:** The OTel `prometheusremotewrite` receiver (v0.142.0+) only supports Remote Write v2.
Prometheus defaults to v1 (`prometheus.WriteRequest`) which is rejected.

**Solution:**
```yaml
# In helm-prometheus-values.yaml, add protobuf_message:
server:
  remoteWrite:
    - url: http://otel-opentelemetry-collector.observability:9090/api/v1/write
      protobuf_message: "io.prometheus.write.v2.Request"
```
Then upgrade and restart:
```bash
helm upgrade prometheus prometheus-community/prometheus \
  -f helm-prometheus-values.yaml -n observability
kubectl rollout restart deployment/prometheus-server -n observability
```

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

```yaml
# In helm-otel-values.yaml, reduce batch sizes:
processors:
  memory_limiter:
    check_interval: 1s
    limit_mib: 400
    spike_limit_mib: 100
  batch:
    timeout: 5s
    send_batch_size: 256      # was 1024
    send_batch_max_size: 512   # add this too
```

Also ensure `GOMEMLIMIT` is set to match `limit_mib`.

---

## Performance Tuning

### OTel Collector

```yaml
# For high throughput:
processors:
  batch:
    timeout: 1s
    send_batch_size: 2048
    send_batch_max_size: 4096

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
          app: otel
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

- **OTel → New Relic:** Secure TLS (default, no `insecure: true`)
- **OTel → Internal backends:** `tls.insecure: true` (Istio mTLS handles encryption at mesh layer)
- **Jaeger:** Internal only, HTTP endpoints (Istio mTLS provides transport encryption)
- **Loki:** Internal only, HTTP endpoints (Istio mTLS provides transport encryption)

> **Note:** For non-Istio deployments, configure explicit TLS certificates for all internal connections.

---

## Version Information

| Component | Image | Chart Version | Status |
|-----------|-------|---------------|--------|
| Jaeger | `jaegertracing/jaeger:2.14.1` | jaeger-4.4.6 | ✅ Supported |
| Loki | `grafana/loki:3.6.4` | loki-6.52.0 | ✅ Supported (official `grafana` repo) |
| OTel Collector | `otel/opentelemetry-collector-contrib:0.145.0` | opentelemetry-collector-0.145.0 | ✅ Supported |
| Prometheus | `quay.io/prometheus/prometheus:v3.9.1` | prometheus-28.9.0 | ✅ Supported |
| Grafana | `grafana/grafana` | grafana | ✅ Supported (`grafana-community` repo) |
| New Relic | Cloud (EU endpoint) | N/A | ✅ Connected |

---

## Support & Documentation

- **Jaeger:** https://www.jaegertracing.io/docs/
- **Loki:** https://grafana.com/docs/loki/
- **OpenTelemetry:** https://opentelemetry.io/docs/
- **Prometheus:** https://prometheus.io/docs/
- **New Relic:** https://docs.newrelic.com/

---

**Document Version:** 2.0 — Production Hardening Update  
**Last Updated:** February 8, 2026  
**Maintaining Team:** Observability Engineering
