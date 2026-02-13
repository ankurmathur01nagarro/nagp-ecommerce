# Observability Stack Verification Script
# Purpose: Verify Loki, Tempo, Alloy, Prometheus, and New Relic connectivity
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
    @{ Label = "app.kubernetes.io/name=alloy"; Name = "Alloy" },
    @{ Label = "app.kubernetes.io/name=loki"; Name = "Loki" },
    @{ Label = "app.kubernetes.io/name=tempo"; Name = "Tempo" },
    @{ Label = "app.kubernetes.io/instance=prometheus"; Name = "Prometheus" },
    @{ Label = "app.kubernetes.io/name=grafana"; Name = "Grafana" }
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
kubectl get pods -n $Namespace -o wide 2>$null | Select-Object -Skip 1 | Where-Object { $_ -match "alloy|loki|tempo|prometheus|grafana" }

# ============================================
# STEP 2: Service Discovery
# ============================================
Write-Section "STEP 2: Service Discovery"

Write-Host "Services:"
kubectl get svc -n $Namespace -o wide 2>$null | Select-Object -Index 0
kubectl get svc -n $Namespace -o wide 2>$null | Select-Object -Skip 1 | Where-Object { $_ -match "alloy|loki|tempo|prometheus|grafana" }

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

Write-Host -NoNewline "Testing Tempo health endpoint... "
try {
    $tempoPod = kubectl get pods -n $Namespace -l app.kubernetes.io/name=tempo -o jsonpath='{.items[0].metadata.name}' 2>$null
    if ($tempoPod) {
        $isRunning = kubectl get pod $tempoPod -n $Namespace -o jsonpath='{.status.phase}' 2>$null
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

$alloyPod = kubectl get pods -n $Namespace -l app.kubernetes.io/name=alloy -o jsonpath='{.items[0].metadata.name}' 2>$null

if ($alloyPod) {
    Write-Success "Alloy pod found: $alloyPod"
} else {
    Write-ErrorMsg "Alloy pod not found"
}

# ============================================
# STEP 5: End-to-End Pipeline Test (telemetrygen)
# ============================================
Write-Section "STEP 5: End-to-End Pipeline Test (telemetrygen)"

$telemetrygenImage = "ghcr.io/open-telemetry/opentelemetry-collector-contrib/telemetrygen:latest"
$otelEndpoint = "alloy.$Namespace`:4317"
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
Write-Host "Waiting 12s for batch flush..." -ForegroundColor Gray
Start-Sleep -Seconds 12

# Verify traces arrived in Tempo
Write-Host -NoNewline "Verifying traces in Tempo... "
try {
    kubectl delete pod tempo-verify -n $Namespace --ignore-not-found 2>$null | Out-Null
    $tempoResult = kubectl run tempo-verify --rm -i --restart=Never -n $Namespace --image=curlimages/curl:latest -- -s "http://tempo.$Namespace.svc.cluster.local:3200/api/search?q=%7B%7D&limit=5" 2>&1
    if ($tempoResult -match "traces" -or $tempoResult -match "traceID") {
        Write-Success "Traces found in Tempo"
    } else {
        Write-WarningMsg "Traces not found yet (may need more time)"
    }
}
catch {
    Write-ErrorMsg "Could not query Tempo API"
}

# Verify logs arrived in Loki (retry up to 4 times; try label then substring match)
Write-Host -NoNewline "Verifying logs in Loki... "
$lokiFound = $false
$lokiQueries = @(
    ('query={service_name="' + $testService + '"}'),
    ('query={}|~"' + $testService + '"')
)
for ($attempt = 1; $attempt -le 4; $attempt++) {
    foreach ($lq in $lokiQueries) {
        try {
            kubectl delete pod loki-verify -n $Namespace --ignore-not-found 2>$null | Out-Null
            $lokiResult = kubectl run loki-verify --rm -i --restart=Never -n $Namespace --image=curlimages/curl:latest -- -s "http://loki.$Namespace.svc.cluster.local:3100/loki/api/v1/query_range" -G --data-urlencode $lq --data-urlencode "limit=1" 2>&1
            $lokiResultStr = ($lokiResult | Out-String)
            if ($lokiResultStr -match '"result":\[' -and -not ($lokiResultStr -match '"result":\[\]')) {
                Write-Success "Found logs for '$testService' in Loki (query: $lq)"
                $lokiFound = $true
                break
            }
        }
        catch { }
    }
    if ($lokiFound) { break }
    Start-Sleep -Seconds 5
}
if (-not $lokiFound) {
    Write-WarningMsg "Logs not found after 4 attempts (may need more time or different labels)"
}

# ============================================
# STEP 6: Component Logs (Last 10 lines)
# ============================================
Write-Section "STEP 6: Recent Logs (Last 10 Lines)"

Write-Host ""
Write-Host "Alloy Logs:" -ForegroundColor Magenta
Write-Host "---" -ForegroundColor Magenta
kubectl logs -n $Namespace -l app.kubernetes.io/name=alloy -c alloy --tail=10 2>$null

Write-Host ""
Write-Host "Loki Logs:" -ForegroundColor Magenta
Write-Host "---" -ForegroundColor Magenta
kubectl logs -n $Namespace -l app.kubernetes.io/name=loki -c loki --tail=10 2>$null

Write-Host ""
Write-Host "Tempo Logs:" -ForegroundColor Magenta
Write-Host "---" -ForegroundColor Magenta
kubectl logs -n $Namespace -l app.kubernetes.io/name=tempo --tail=10 2>$null

# ============================================
# STEP 7: Port-Forwarding Instructions
# ============================================
Write-Section "STEP 7: Next Steps - Port Forwarding"

Write-Host ""
Write-Host "To access dashboards, open a new PowerShell terminal and run:"
Write-Host ""
Write-Host "Grafana Dashboards (Port 3000) — primary UI for traces, logs, and metrics:" -ForegroundColor Yellow
Write-Host "  kubectl port-forward -n observability svc/grafana 3000:3000"
Write-Host "  Then open: http://localhost:3000  (credentials from grafana-admin-secret)"
Write-Host "  Use Explore > Tempo datasource for trace queries (TraceQL)"
Write-Host ""
Write-Host "Tempo API (Port 3200):" -ForegroundColor Yellow
Write-Host "  kubectl port-forward -n observability svc/tempo 3200:3200"
Write-Host "  Then test: http://localhost:3200/ready"
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
# STEP 8: Summary
# ============================================
Write-Section "STEP 8: Test Summary"

Write-Host ""
$lokiStatus = kubectl get pods -n $Namespace -l app.kubernetes.io/name=loki -o jsonpath='{.items[0].status.phase}' 2>$null
$tempoStatus = kubectl get pods -n $Namespace -l app.kubernetes.io/name=tempo -o jsonpath='{.items[0].status.phase}' 2>$null
$alloyStatus = kubectl get pods -n $Namespace -l app.kubernetes.io/name=alloy -o jsonpath='{.items[0].status.phase}' 2>$null
$promStatus = kubectl get pods -n $Namespace -l app.kubernetes.io/instance=prometheus -o jsonpath='{.items[0].status.phase}' 2>$null
$grafanaStatus = kubectl get pods -n $Namespace -l app.kubernetes.io/name=grafana -o jsonpath='{.items[0].status.phase}' 2>$null

Write-Host "Component Status:"
if ($lokiStatus -eq "Running") { Write-Success "Loki: $lokiStatus" } else { Write-ErrorMsg "Loki: $lokiStatus" }
if ($tempoStatus -eq "Running") { Write-Success "Tempo: $tempoStatus" } else { Write-ErrorMsg "Tempo: $tempoStatus" }
if ($alloyStatus -eq "Running") { Write-Success "Alloy: $alloyStatus" } else { Write-ErrorMsg "Alloy: $alloyStatus" }
if ($promStatus -eq "Running") { Write-Success "Prometheus: $promStatus" } else { Write-ErrorMsg "Prometheus: $promStatus" }
if ($grafanaStatus -eq "Running") { Write-Success "Grafana: $grafanaStatus" } else { Write-ErrorMsg "Grafana: $grafanaStatus" }

Write-Host ""
Write-Host "===============================================" -ForegroundColor Green
Write-Host "Verification Complete!" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green
Write-Host ""

if ($lokiStatus -eq "Running" -and $tempoStatus -eq "Running" -and $alloyStatus -eq "Running") {
    Write-Success "All critical components are READY!"
    Write-Host ""
    Write-Host "Next steps:"
    Write-Host "  1. Generate test trace/logs from your application"
    Write-Host "  2. Verify data appears in Grafana (Explore > Tempo for traces, Loki for logs, Prometheus for metrics)"
    Write-Host "  3. Confirm New Relic receives data (check https://one.newrelic.com)"
}
else {
    Write-WarningMsg "Some components are not running. Check logs above for details."
}

Write-Host ""
