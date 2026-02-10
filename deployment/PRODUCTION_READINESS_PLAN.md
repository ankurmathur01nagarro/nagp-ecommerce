# Production Readiness Plan — Observability Stack

> **Created:** 2026-02-08  
> **Last Updated:** 2026-02-08  
> **Status:** In Progress — P0 complete, P1 mostly complete, P2 pending  
> **Current Environment:** k3d dev cluster (1 server + 2 agents), namespace `observability`  
> **Target:** Production-grade observability on constrained infrastructure

---

## Table of Contents

1. [Completed Items Summary](#completed-items-summary)
2. [Pending Items](#pending-items)
3. [Production Architecture Diagram](#production-architecture-diagram)
4. [Resource Estimates — Dev vs Prod](#resource-estimates--dev-vs-prod)

---

## Completed Items Summary

All P0 (Critical) items and most P1 (Important) items have been implemented.  
Full configuration details are documented in [CONFIGURATION_MANUAL.md](CONFIGURATION_MANUAL.md).

### P0 — Critical (All Complete ✅)

| ID | Item | Resolution | Helm Rev |
|----|------|------------|----------|
| P0-1 | Jaeger v2 config used invalid v1-style env vars | Complete rewrite to `userconfig` block (OTel Collector-style YAML) with `jaeger_storage`, `healthcheckv2` on port 13133, `max_traces: 5000` | jaeger rev 12 |
| P0-2 | Debug exporter active on all pipelines | Removed `debug` from all pipeline exporters (commented out for dev troubleshooting) | alloy rev 1 |
| P0-3 | No `memory_limiter` processor | Added `memory_limiter` (limit_mib: 400, spike_limit_mib: 100) as first processor in all pipelines + `GOMEMLIMIT=400MiB` env var | alloy rev 1 |
| P0-4 | 100% trace sampling rate | Reduced `randomSamplingPercentage` from 100 to 5 in Istio Telemetry | kubectl applied |
| P0-5 | Secrets hardcoded in source control | Replaced hardcoded API key with `%NEW_RELIC_API_KEY%` env var with validation in `create-cluster.bat` | script updated |

### P1 — Important (Mostly Complete)

| ID | Item | Resolution | Helm Rev |
|----|------|------------|----------|
| P1-1 | Loki has no retention policy | Added `retention_period: 168h` + compactor with `retention_enabled: true`, `working_directory: /var/loki/compactor` | loki rev 2 |
| P1-2 | Prometheus has no resource limits | Added resources (250m/1 CPU, 512Mi/2Gi memory), `retention: 15d`, `retentionSize: 8GB` | prometheus rev 9 |
| P1-4 | TLS disabled on New Relic exporter | Removed `tls.insecure: true` from New Relic exporter (defaults to secure TLS). Internal exporters keep `insecure: true` (Istio mTLS at mesh layer) | alloy rev 1 |
| P1-5 | Jaeger runs as root (UID 0) | Added `runAsNonRoot: true, runAsUser: 10001` (matches Jaeger v2 Dockerfile) | jaeger rev 12 |
| P1-7 | Grafana hardcoded credentials | Replaced `adminUser/adminPassword` with `admin.existingSecret: grafana-admin-secret` | grafana rev 2 |

### P2 — Recommended (Partially Complete)

| ID | Item | Resolution | Helm Rev |
|----|------|------------|----------|
| P2-3 | No Grafana persistence | Enabled persistence (1Gi PVC, `storageClassName: local-path`) | grafana rev 2 |
| P2-7 | Grafana chart migrated | Updated `create-cluster.bat` to use `grafana-community` repo | script updated |
| P2-8 | Loki chart repo incorrect | Migrated Loki from `grafana-community/loki` to official `grafana/loki` repo | script updated |
| P2-9 | Imperative observability deploys | Migrated all observability Helm releases to ArgoCD App-of-Apps with multi-source apps, sync waves, and separate `observability` AppProject | GitOps |

---

## Pending Items

### P1-3: Prometheus Remote Write Queue Tuning

**Status:** Deferred — defaults already generous  
**Effort:** Low | **Impact:** Medium

> **Verified against:** [Prometheus remote_write config](https://prometheus.io/docs/prometheus/latest/configuration/configuration/#remote_write)  
> Prometheus v3.9.1 defaults: `capacity: 10000`, `max_shards: 50`, `max_samples_per_send: 2000`.

The current defaults are already generous for most workloads. Only tune if you observe:
- `prometheus_remote_storage_pending_samples` consistently near capacity
- `prometheus_remote_storage_dropped_samples_total` increasing

**If needed:**
```yaml
server:
  remoteWrite:
    - url: http://alloy.observability:9090/api/v1/metrics/write
      protobuf_message: "io.prometheus.write.v2.Request"
      queue_config:
        capacity: 10000
        max_shards: 50
        min_shards: 1
        max_samples_per_send: 2000
        batch_send_deadline: 5s
      write_relabel_configs:         # optional: drop noisy metrics
        - source_labels: [__name__]
          regex: "go_.*"
          action: drop
```

---

### P1-6: Single Replica for All Components

**Status:** Pending — requires shared storage infrastructure  
**Effort:** High | **Impact:** High — availability

Every component has `replicas: 1`. Any pod restart = downtime for that signal.

**Recommended minimum HA:**

| Component | Dev Replicas | Prod Replicas | Notes |
|---|---|---|---|
| Grafana Alloy | 1 | 2–3 | Stateless, easy to scale |
| Jaeger | 1 | 2+ (with shared storage) | Requires Elasticsearch or shared BadgerDB |
| Loki | 1 (SingleBinary) | 3 (read/write/backend) | Switch to microservices mode |
| Prometheus | 1 | 2 (with Thanos sidecar) | Or use kube-prometheus-stack |
| Grafana | 1 | 2+ | With shared DB for dashboards |

---

### P2-1: Local-Path StorageClass

**Status:** Pending — requires production infrastructure  
**Effort:** Medium | **Impact:** High

> **Verified against:** [grafana/loki Helm chart](https://github.com/grafana/loki/blob/main/production/helm/loki/values.yaml)  
> Helm chart uses `bucketNames` (camelCase) with `.chunks`/`.ruler` sub-keys.

Current `storageClass: local-path` (k3d default) stores data on the node's local disk.
If the node dies, data is lost.

**Fix:** Use a production-grade StorageClass:
- **Cloud:** `gp3` (AWS), `Premium_LRS` (Azure), `pd-ssd` (GCP)
- **On-prem:** Longhorn, Rook-Ceph, OpenEBS

**Object storage for Loki (Helm chart syntax):**
```yaml
loki:
  storage:
    type: s3
    bucketNames:                       # camelCase in Helm chart
      chunks: loki-chunks
      ruler: loki-ruler
    s3:
      endpoint: minio.storage.svc:9000
      accessKeyId: ${MINIO_ACCESS_KEY}
      secretAccessKey: ${MINIO_SECRET_KEY}
      s3ForcePathStyle: true
      insecure: true
```

---

### P2-2: Loki Caches Disabled

**Status:** Pending — deploys additional memcached pods  
**Effort:** Low | **Impact:** Medium — query performance

> **Verified against:** [grafana/loki Helm chart](https://github.com/grafana/loki/blob/main/production/helm/loki/values.yaml)  
> `chunksCache` and `resultsCache` are top-level chart values that deploy external
> memcached StatefulSets. `allocatedMemory` is in MB.

```yaml
chunksCache:
  enabled: true
  allocatedMemory: 512   # MB — deploys a memcached StatefulSet (chart default: 8192)
resultsCache:
  enabled: true
  allocatedMemory: 256   # MB — deploys a memcached StatefulSet (chart default: 1024)
```

> **Warning:** Enabling caches deploys additional memcached pods. On a constrained k3d cluster,
> this may cause resource pressure. Consider enabling only `chunksCache` initially.

---

### P2-4: No NetworkPolicies

**Status:** Pending  
**Effort:** Medium | **Impact:** Medium — security

Any pod in the cluster can reach observability services.

**Fix:** Create NetworkPolicies to restrict traffic:
```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: alloy-ingress
  namespace: observability
spec:
  podSelector:
    matchLabels:
      app.kubernetes.io/name: alloy
  ingress:
    - from:
        - namespaceSelector:
            matchLabels:
              kubernetes.io/metadata.name: nagp-ecom
        - namespaceSelector:
            matchLabels:
              kubernetes.io/metadata.name: istio-system
      ports:
        - port: 4317
        - port: 4318
        - port: 9090
  policyTypes:
    - Ingress
```

---

### P2-5: No PodDisruptionBudgets or HorizontalPodAutoscalers

**Status:** Pending  
**Effort:** Low | **Impact:** Medium — resilience

**PDB example:**
```yaml
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: alloy-pdb
  namespace: observability
spec:
  minAvailable: 1
  selector:
    matchLabels:
      app.kubernetes.io/name: alloy
```

**HPA example (Grafana Alloy):**
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: alloy-hpa
  namespace: observability
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: alloy
  minReplicas: 2
  maxReplicas: 5
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
    - type: Resource
      resource:
        name: memory
        target:
          type: Utilization
          averageUtilization: 80
```

---

### P2-6: Alloy Batch Config May Be Undersized

**Status:** Pending  
**Effort:** Low | **Impact:** Low — throughput optimization

Current batch config (per-signal in Alloy syntax):
```
otelcol.processor.batch "traces" {
  timeout         = "5s"
  send_batch_size = 1024
}
```

**Recommendation for production (adjust in `helm-alloy-values.yaml` configMap.content):**
```
otelcol.processor.batch "traces" {
  timeout         = "5s"
  send_batch_size = 2048
}
```

---

## Production Architecture Diagram

```
                    ┌─────────────────────────────────────────────┐
                    │              Application Pods                │
                    │         (Istio ambient mesh, 5% sampling)   │
                    └──────────────────┬──────────────────────────┘
                                       │ OTLP (gRPC :4317 / HTTP :4318)
                                       ▼
                    ┌──────────────────────────────────────────────┐
                    │     Grafana Alloy (2-3 replicas, HPA)        │
                    │  ┌────────────┐ ┌────────────┐ ┌──────────┐ │
                    │  │memory_limit│→│   batch     │→│ exporters│ │
                    │  └────────────┘ └────────────┘ └──────────┘ │
                    └───┬──────────────┬──────────────┬───────────┘
                        │              │              │
               Traces   │     Logs     │    Metrics   │
                        ▼              ▼              ▼
              ┌──────────────┐  ┌────────────┐  ┌──────────────┐
              │   Jaeger     │  │   Loki     │  │  Prometheus  │
              │ (Elasticsearch│  │ (S3/MinIO) │  │  (2Gi mem)   │
              │  backend)    │  │  retention  │  │  15d retain  │
              │  50Gi+ PVC   │  │  7d, 50Gi  │  │  8GB cap     │
              └──────┬───────┘  └─────┬──────┘  └──────┬───────┘
                     │                │                 │
                     └────────────────┼─────────────────┘
                                      ▼
                            ┌──────────────────┐
                            │    Grafana (HA)   │
                            │  (persistent DB,  │
                            │   secret auth)    │
                            └──────────────────┘
                                      │
                     ┌────────────────┼────────────────┐
                     ▼                                 ▼
              ┌────────────┐                   ┌────────────────┐
              │  New Relic  │                   │  NetworkPolicy │
              │ (external,  │                   │  + PDB + HPA   │
              │  TLS valid) │                   └────────────────┘
              └────────────┘
```

---

## Resource Estimates — Dev vs Prod

| Component | Dev CPU (req/lim) | Dev Mem (req/lim) | Prod CPU (req/lim) | Prod Mem (req/lim) |
|---|---|---|---|---|
| Grafana Alloy | 100m/500m | 128Mi/512Mi | 250m/1 | 256Mi/1Gi |
| Jaeger | 100m/500m | 128Mi/512Mi | 250m/1 | 256Mi/2Gi |
| Loki | 100m/200m | 128Mi/256Mi | 250m/500m | 256Mi/1Gi |
| Prometheus | 250m/1 | 512Mi/2Gi | 250m/1 | 512Mi/2Gi |
| Grafana | 100m/500m | 128Mi/256Mi | 100m/500m | 128Mi/512Mi |
| **Total (single replica)** | **~700m/2.7** | **~1Gi/3.5Gi** | **~1.1/4** | **~1.4Gi/6.5Gi** |

> **Note:** Production totals assume single replica per component. Multiply by replica count
> for true cluster resource requirements.

---

## Pending Implementation Checklist

| Priority | ID | Item | Effort | Status |
|----------|-----|------|--------|--------|
| P1 | P1-3 | Prometheus remote write queue tuning | Low | Deferred (defaults sufficient) |
| P1 | P1-6 | Scale components to 2+ replicas | High | Requires shared storage |
| P2 | P2-1 | Production StorageClass / object storage for Loki | Medium | Requires prod infra |
| P2 | P2-2 | Enable Loki caches (chunksCache + resultsCache) | Low | Deploys memcached pods |
| P2 | P2-4 | Create NetworkPolicies | Medium | |
| P2 | P2-5 | PodDisruptionBudgets + HorizontalPodAutoscalers | Low | |
| P2 | P2-6 | Increase Alloy batch size | Low | |

---

## Files To Modify (Pending Items)

| File | Changes Required | Priority Items |
|---|---|---|
| `helm-prometheus-values.yaml` | Queue config (if needed) | P1-3 |
| `helm-alloy-values.yaml` | Increase batch size, scale replicas | P2-6, P1-6 |
| `helm-loki-values.yaml` | Enable caches, increase PVC, change StorageClass | P2-1, P2-2 |
| `helm-jaeger-values.yaml` | Switch to Elasticsearch/BadgerDB, scale replicas | P1-6, P2-1 |
| `helm-grafana-values.yaml` | Change StorageClass, scale replicas | P2-1, P1-6 |
| New: `networkpolicy.yaml` | Restrict namespace access | P2-4 |
| New: `pdb-hpa.yaml` | PDB + HPA manifests | P2-5 |
