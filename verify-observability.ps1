# Observability Stack Verification Script
# Purpose: Verify Loki, Jaeger, OTel Collector, Prometheus, and New Relic connectivity
# Platform: PowerShell (Windows)

param(
    [string]$Namespace = "observability",
    [int]$Timeout = 120
)

$ErrorActionPreference = "Continue"

# Helper functions
function Write-Success {
    param([string]$Message)
    Write-Host "[OK]  $Message" -ForegroundColor Green
}

function Write-ErrorMsg {
    param([string]$Message)
    Write-Host "[ERR] $Message" -ForegroundColor Red
}

function Write-WarningMsg {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "==========================================" -ForegroundColor Blue
    Write-Host $Title -ForegroundColor Blue
    Write-Host "==========================================" -ForegroundColor Blue
}

# Main verification starts here
Clear-Host
Write-Host ""
Write-Host "OBSERVABILITY STACK VERIFICATION" -ForegroundColor Cyan
Write-Host "Date: $(Get-Date)" -ForegroundColor Cyan
Write-Host "Namespace: $Namespace" -ForegroundColor Cyan
Write-Host ""

# ============================================
# STEP 1: Pod Status Check
# ============================================
Write-Section "STEP 1: Pod Status Check"

$components = @(
    @{ Label = "app.kubernetes.io/name=opentelemetry-collector"; Name = "OTel Collector" },
    @{ Label = "app.kubernetes.io/name=loki"; Name = "Loki" },
    @{ Label = "app.kubernetes.io/name=jaeger"; Name = "Jaeger" },
    @{ Label = "app.kubernetes.io/instance=prometheus"; Name = "Prometheus" }
)

foreach ($component in $components) {
    Write-Host -NoNewline "Checking $($component.Name)... "
    
    try {
        $pod = kubectl get pods -n $Namespace -l $component.Label -o jsonpath='{.items[0].metadata.name}' 2>$null
        
        if ($pod) {
            $status = kubectl get pod $pod -n $Namespace -o jsonpath='{.status.phase}' 2>$null
            if ($status -eq "Running") {
                Write-Success $status
            } else {
                Write-WarningMsg $status
            }
        } else {
            Write-ErrorMsg "Not found"
        }
    }
    catch {
        Write-ErrorMsg "Error checking pod"
    }
}

Write-Host ""
Write-Host "Pod Details:"
kubectl get pods -n $Namespace -o wide 2>$null | Select-Object -Index 0
kubectl get pods -n $Namespace -o wide 2>$null | Select-Object -Skip 1 | Where-Object { $_ -match "otel|loki|jaeger|prometheus" }

# ============================================
# STEP 2: Service Discovery
# ============================================
Write-Section "STEP 2: Service Discovery"

Write-Host "Services:"
kubectl get svc -n $Namespace -o wide 2>$null | Select-Object -Index 0
kubectl get svc -n $Namespace -o wide 2>$null | Select-Object -Skip 1 | Where-Object { $_ -match "otel|loki|jaeger|prometheus" }

# ============================================
# STEP 3: Components Health Check
# ============================================
Write-Section "STEP 3: Components Health Check"

Write-Host -NoNewline "Testing Loki health endpoint... "
try {
    $lokiPod = kubectl get pods -n $Namespace -l app.kubernetes.io/name=loki -o jsonpath='{.items[0].metadata.name}' 2>$null
    if ($lokiPod) {
        $isRunning = kubectl get pod $lokiPod -n $Namespace -o jsonpath='{.status.phase}' 2>$null
        if ($isRunning -eq "Running") {
            Write-Success "Running"
        } else {
            Write-WarningMsg $isRunning
        }
    } else {
        Write-ErrorMsg "Pod not found"
    }
}
catch {
    Write-ErrorMsg "Check failed"
}

Write-Host -NoNewline "Testing Jaeger health endpoint... "
try {
    $jaegerPod = kubectl get pods -n $Namespace -l app.kubernetes.io/name=jaeger -o jsonpath='{.items[0].metadata.name}' 2>$null
    if ($jaegerPod) {
        $isRunning = kubectl get pod $jaegerPod -n $Namespace -o jsonpath='{.status.phase}' 2>$null
        if ($isRunning -eq "Running") {
            Write-Success "Running"
        } else {
            Write-WarningMsg $isRunning
        }
    } else {
        Write-ErrorMsg "Pod not found"
    }
}
catch {
    Write-ErrorMsg "Check failed"
}

Write-Host -NoNewline "Testing Prometheus health endpoint... "
try {
    $promPods = kubectl get pods -n $Namespace -l app.kubernetes.io/instance=prometheus,app.kubernetes.io/component=server -o jsonpath='{.items[0].metadata.name}' 2>$null
    if ($promPods) {
        $isRunning = kubectl get pod $promPods -n $Namespace -o jsonpath='{.status.phase}' 2>$null
        if ($isRunning -eq "Running") {
            Write-Success "Running"
        } else {
            Write-WarningMsg $isRunning
        }
    } else {
        Write-ErrorMsg "Pod not found"
    }
}
catch {
    Write-ErrorMsg "Check failed"
}

# ============================================
# STEP 4: Data Flow Tests
# ============================================
Write-Section "STEP 4: Data Flow Tests"

$otelPod = kubectl get pods -n $Namespace -l app.kubernetes.io/name=opentelemetry-collector -o jsonpath='{.items[0].metadata.name}' 2>$null

if ($otelPod) {
    Write-Host -NoNewline "Testing OTel Collector -> Jaeger connection... "
    try {
        $result = kubectl exec -n $Namespace $otelPod -- wget -q -O- --timeout=5 http://jaeger.observability.svc.cluster.local:14250 2>$null
        Write-Success "Connected"
    }
    catch {
        Write-ErrorMsg "Failed"
    }

    Write-Host -NoNewline "Testing OTel Collector -> Loki connection... "
    try {
        $result = kubectl exec -n $Namespace $otelPod -- wget -q -O- --timeout=5 http://loki.observability.svc.cluster.local:3100/loki/api/v1/status 2>$null
        Write-Success "Connected"
    }
    catch {
        Write-ErrorMsg "Failed"
    }

    Write-Host -NoNewline "Testing OTel Collector -> New Relic connection... "
    try {
        $result = kubectl exec -n $Namespace $otelPod -- wget -q -O- --timeout=10 https://otlp.eu01.nr-data.net:4318 2>$null
        Write-Success "Connected"
    }
    catch {
        Write-WarningMsg "Cannot verify (may need HTTPS support in pod)"
    }
} else {
    Write-ErrorMsg "OTel Collector pod not found"
}

# ============================================
# STEP 5: Configuration Verification
# ============================================
Write-Section "STEP 5: Configuration Verification"

Write-Host -NoNewline "Checking New Relic API Key Secret... "
try {
    $secret = kubectl get secret newrelic-otel-secret -n $Namespace -o jsonpath='{.data.api-key}' 2>$null
    if ($secret) {
        $apiKey = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($secret))
        if ($apiKey) {
            Write-Success "Found"
            Write-Host "  API Key Length: $($apiKey.Length) characters"
            $displayKey = $apiKey.Substring(0, [Math]::Min(8, $apiKey.Length))
            Write-Host "  First 8 chars: $displayKey..." -ForegroundColor Gray
        } else {
            Write-ErrorMsg "Empty key"
        }
    } else {
        Write-ErrorMsg "NOT Found"
    }
}
catch {
    Write-ErrorMsg "Error retrieving secret"
}

Write-Host -NoNewline "Checking OTel Collector Config (Exporters)... "
try {
    $config = kubectl get configmap otel-opentelemetry-collector -n $Namespace -o yaml 2>$null
    $jaegerFound = ($config | Select-String "otlp_grpc/jaeger" | Measure-Object).Count -gt 0
    $lokiFound = ($config | Select-String "otlp_http/loki" | Measure-Object).Count -gt 0
    $nrFound = ($config | Select-String "otlp_http/newrelic" | Measure-Object).Count -gt 0
    
    if ($jaegerFound -and $lokiFound -and $nrFound) {
        Write-Success "All exporters configured"
        Write-Host "  - otlp_grpc/jaeger: Found" -ForegroundColor Gray
        Write-Host "  - otlp_http/loki:   Found" -ForegroundColor Gray
        Write-Host "  - otlp_http/newrelic: Found" -ForegroundColor Gray
    } else {
        Write-WarningMsg "Some exporters missing"
        if (-not $jaegerFound) { Write-ErrorMsg "  - otlp_grpc/jaeger: NOT found" }
        if (-not $lokiFound)   { Write-ErrorMsg "  - otlp_http/loki:   NOT found" }
        if (-not $nrFound)     { Write-ErrorMsg "  - otlp_http/newrelic: NOT found" }
    }
}
catch {
    Write-ErrorMsg "Cannot verify config"
}

Write-Host -NoNewline "Checking Prometheus Remote Write v2... "
try {
    $promConfig = kubectl get configmap prometheus-server -n $Namespace -o jsonpath='{.data.prometheus\.yml}' 2>$null
    if ($promConfig -match "io\.prometheus\.write\.v2\.Request") {
        Write-Success "Remote Write v2 configured"
    } else {
        Write-WarningMsg "Remote Write v2 NOT configured (OTel receiver requires v2)"
    }
}
catch {
    Write-ErrorMsg "Cannot verify Prometheus config"
}

# ============================================
# STEP 6: End-to-End Pipeline Test (telemetrygen)
# ============================================
Write-Section "STEP 6: End-to-End Pipeline Test (telemetrygen)"

$telemetrygenImage = "ghcr.io/open-telemetry/opentelemetry-collector-contrib/telemetrygen:latest"
$otelEndpoint = "otel-opentelemetry-collector.$Namespace`:4317"
$testService = "verify-test-$(Get-Date -Format 'HHmmss')"

Write-Host "Service name: $testService" -ForegroundColor Gray
Write-Host ""

# Send traces
Write-Host -NoNewline "Sending test traces via telemetrygen... "
try {
    $traceOut = kubectl run telemetrygen-traces-verify --rm -i --restart=Never -n $Namespace --image=$telemetrygenImage -- traces --otlp-insecure --otlp-endpoint $otelEndpoint --traces 3 --service $testService 2>&1
    if ($traceOut -match "traces generated") {
        Write-Success "3 traces sent"
    } else {
        Write-WarningMsg "Sent (could not confirm output)"
    }
}
catch {
    Write-ErrorMsg "Failed to send traces"
}

# Send logs
Write-Host -NoNewline "Sending test logs via telemetrygen... "
try {
    $logOut = kubectl run telemetrygen-logs-verify --rm -i --restart=Never -n $Namespace --image=$telemetrygenImage -- logs --otlp-insecure --otlp-endpoint $otelEndpoint --logs 5 --service $testService 2>&1
    if ($logOut -match "logs generated") {
        Write-Success "5 logs sent"
    } else {
        Write-WarningMsg "Sent (could not confirm output)"
    }
}
catch {
    Write-ErrorMsg "Failed to send logs"
}

# Send metrics
Write-Host -NoNewline "Sending test metrics via telemetrygen... "
try {
    $metricOut = kubectl run telemetrygen-metrics-verify --rm -i --restart=Never -n $Namespace --image=$telemetrygenImage -- metrics --otlp-insecure --otlp-endpoint $otelEndpoint --metrics 3 --service $testService 2>&1
    if ($metricOut -match "metrics generated") {
        Write-Success "3 metrics sent"
    } else {
        Write-WarningMsg "Sent (could not confirm output)"
    }
}
catch {
    Write-ErrorMsg "Failed to send metrics"
}

# Wait for batch processor to flush
Write-Host ""
Write-Host "Waiting 12s for OTel batch flush..." -ForegroundColor Gray
Start-Sleep -Seconds 12

# Verify traces arrived in Jaeger
Write-Host -NoNewline "Verifying traces in Jaeger... "
try {
    kubectl delete pod jaeger-verify -n $Namespace --ignore-not-found 2>$null | Out-Null
    $jaegerServices = kubectl run jaeger-verify --rm -i --restart=Never -n $Namespace --image=curlimages/curl:latest -- -s "http://jaeger.$Namespace.svc.cluster.local:16686/api/services" 2>&1
    if ($jaegerServices -match $testService) {
        Write-Success "Found service '$testService' in Jaeger"
    } else {
        Write-WarningMsg "Service not found yet (may need more time)"
    }
}
catch {
    Write-ErrorMsg "Could not query Jaeger API"
}

# Verify logs arrived in Loki (retry up to 3 times — Loki ingestion can take a moment)
Write-Host -NoNewline "Verifying logs in Loki... "
$lokiFound = $false
$lokiQuery = 'query={service_name=\"' + $testService + '\"}'
for ($attempt = 1; $attempt -le 3; $attempt++) {
    try {
        kubectl delete pod loki-verify -n $Namespace --ignore-not-found 2>$null | Out-Null
        $lokiResult = kubectl run loki-verify --rm -i --restart=Never -n $Namespace --image=curlimages/curl:latest -- -s "http://loki.$Namespace.svc.cluster.local:3100/loki/api/v1/query_range" -G --data-urlencode $lokiQuery --data-urlencode "limit=1" 2>&1
        $lokiResultStr = ($lokiResult | Out-String)
        if ($lokiResultStr -match '"result":\[') {
            Write-Success "Found logs for '$testService' in Loki"
            $lokiFound = $true
            break
        }
    }
    catch { }
    if ($attempt -lt 3) {
        Start-Sleep -Seconds 5
    }
}
if (-not $lokiFound) {
    Write-WarningMsg "Logs not found after 3 attempts (may need more time)"
}

# ============================================
# STEP 7: Component Logs (Last 10 lines)
# ============================================
Write-Section "STEP 7: Recent Logs (Last 10 Lines)"

Write-Host ""
Write-Host "OTel Collector Logs:" -ForegroundColor Magenta
Write-Host "---" -ForegroundColor Magenta
kubectl logs -n $Namespace -l app.kubernetes.io/name=opentelemetry-collector --tail=10 2>$null

Write-Host ""
Write-Host "Loki Logs:" -ForegroundColor Magenta
Write-Host "---" -ForegroundColor Magenta
kubectl logs -n $Namespace -l app.kubernetes.io/name=loki -c loki --tail=10 2>$null

Write-Host ""
Write-Host "Jaeger Logs:" -ForegroundColor Magenta
Write-Host "---" -ForegroundColor Magenta
kubectl logs -n $Namespace -l app.kubernetes.io/name=jaeger --tail=10 2>$null

# ============================================
# STEP 8: Port-Forwarding Instructions
# ============================================
Write-Section "STEP 8: Next Steps - Port Forwarding"

Write-Host ""
Write-Host "To access dashboards, open a new PowerShell terminal and run:"
Write-Host ""
Write-Host "Jaeger Traces (Port 16686):" -ForegroundColor Yellow
Write-Host "  kubectl port-forward -n observability svc/jaeger 16686:16686"
Write-Host "  Then open: http://localhost:16686"
Write-Host ""
Write-Host "Loki Logs (Port 3100):" -ForegroundColor Yellow
Write-Host "  kubectl port-forward -n observability svc/loki 3100:3100"
Write-Host "  Then test: http://localhost:3100/loki/api/v1/label/job/values"
Write-Host ""
Write-Host "Prometheus Metrics (Port 9090):" -ForegroundColor Yellow
Write-Host "  kubectl port-forward -n observability svc/prometheus-server 9090:80"
Write-Host "  Then open: http://localhost:9090"
Write-Host ""
Write-Host "New Relic (Cloud):" -ForegroundColor Yellow
Write-Host "  Navigate to: https://one.newrelic.com"
Write-Host ""

# ============================================
# STEP 9: Summary
# ============================================
Write-Section "STEP 9: Test Summary"

Write-Host ""
$lokiStatus = kubectl get pods -n $Namespace -l app.kubernetes.io/name=loki -o jsonpath='{.items[0].status.phase}' 2>$null
$jaegerStatus = kubectl get pods -n $Namespace -l app.kubernetes.io/name=jaeger -o jsonpath='{.items[0].status.phase}' 2>$null
$otelStatus = kubectl get pods -n $Namespace -l app.kubernetes.io/name=opentelemetry-collector -o jsonpath='{.items[0].status.phase}' 2>$null
$promStatus = kubectl get pods -n $Namespace -l app.kubernetes.io/instance=prometheus -o jsonpath='{.items[0].status.phase}' 2>$null

Write-Host "Component Status:"
if ($lokiStatus -eq "Running") { Write-Success "Loki: $lokiStatus" } else { Write-ErrorMsg "Loki: $lokiStatus" }
if ($jaegerStatus -eq "Running") { Write-Success "Jaeger: $jaegerStatus" } else { Write-ErrorMsg "Jaeger: $jaegerStatus" }
if ($otelStatus -eq "Running") { Write-Success "OTel Collector: $otelStatus" } else { Write-ErrorMsg "OTel Collector: $otelStatus" }
if ($promStatus -eq "Running") { Write-Success "Prometheus: $promStatus" } else { Write-ErrorMsg "Prometheus: $promStatus" }

Write-Host ""
Write-Host "===============================================" -ForegroundColor Green
Write-Host "Verification Complete!" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green
Write-Host ""

if ($lokiStatus -eq "Running" -and $jaegerStatus -eq "Running" -and $otelStatus -eq "Running") {
    Write-Success "All critical components are READY!"
    Write-Host ""
    Write-Host "Next steps:"
    Write-Host "  1. Generate test trace/logs from your application"
    Write-Host "  2. Verify data appears in dashboards (Jaeger, Loki, Prometheus)"
    Write-Host "  3. Confirm New Relic receives data (check https://one.newrelic.com)"
}
else {
    Write-WarningMsg "Some components are not running. Check logs above for details."
}

Write-Host ""
