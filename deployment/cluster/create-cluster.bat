SET ArgoCDAdminPassword=admin
k3d cluster create local --agents 2 --port "80:80@server:0" --port "443:443@server:0" --port "8000:8000@server:0" --k3s-arg "--disable=traefik@server:0" --api-port 6555
kubectl config use-context k3d-local

echo ================================================================
echo Install Tools: Istioctl, argocd CLI, helm, openssl using scoop
echo ================================================================
scoop install istioctl
scoop install argocd
scoop install helm

echo ================================================================
echo Install Gateway API, ArgoCD CRDs
echo ================================================================
kubectl apply --server-side -f "https://github.com/kubernetes-sigs/gateway-api/releases/download/v1.4.1/standard-install.yaml"

helm repo add argo https://argoproj.github.io/argo-helm
helm repo update

echo ================================================================
echo Install Istio
echo ================================================================
istioctl install -f .\deployment\istio-config.yaml --set values.global.platform=k3d -y
kubectl apply -f https://raw.githubusercontent.com/istio/istio/release-1.28/samples/addons/kiali.yaml

echo ================================================================
echo Pre-create namespaces and secrets (required before ArgoCD syncs)
echo ================================================================
kubectl create namespace observability
rem Create New Relic secret for OpenTelemetry Collector
rem API key must be set via environment variable NEW_RELIC_API_KEY (do NOT hardcode in source control)
if "%NEW_RELIC_API_KEY%"=="" (
    echo ERROR: NEW_RELIC_API_KEY environment variable is not set.
    echo Set it with: set NEW_RELIC_API_KEY=your-api-key-here
    exit /b 1
)
kubectl create secret generic newrelic-otel-secret --from-literal=api-key=%NEW_RELIC_API_KEY% -n observability
rem Create Grafana admin secret (password should be changed after first login)
kubectl create secret generic grafana-admin-secret --from-literal=admin-user=admin --from-literal=admin-password=changeme -n observability

echo ================================================================
echo Install ArgoCD (with Application health check for sync waves)
echo ================================================================
kubectl create namespace argocd
argocd account bcrypt --password %ArgoCDAdminPassword%

helm install argocd argo/argo-cd -n argocd -f .\deployment\helm-argocd-values.yaml

echo ==================== Access ArgoCD UI ==========================
echo kubectl port-forward service/argocd-server -n argocd 8080:443
echo ================================================================

echo ================================================================
echo Install ArgoCD Application that contains all (Apps of App Pattern)
echo ================================================================
@rem Create namespace for application with istio ambient mode labels
kubectl create namespace nagp-ecom --labels istio.io/dataplane-mode=ambient
kubectl apply -f .\deployment\scripts\application.yaml
