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
helm repo add open-telemetry https://open-telemetry.github.io/opentelemetry-helm-charts
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update

echo ================================================================
echo Install Istio
echo ================================================================
istioctl install -f .\deployment\istio-config.yaml --set values.global.platform=k3d -y
kubectl apply -f deployment/istio-resources.k8s.yaml
kubectl apply -f https://raw.githubusercontent.com/istio/istio/release-1.28/samples/addons/kiali.yaml

kubectl create namespace observability
rem Create New Relic secret for OpenTelemetry Collector
kubectl create secret generic newrelic-otel-secret --from-literal=api-key=eu01xx464c0a4003a336f553b4808643FFFFNRAL -n observability

rem Install OpenTelemetry Collector via Helm
helm install otel open-telemetry/opentelemetry-collector -f .\deployment\helm-otel-values.yaml -n observability
kubectl wait -n observability --for=condition=ready pod -l app.kubernetes.io/name=opentelemetry-collector --timeout=120s

rem Install Prometheus via Helm with remote_write to OTel Collector
helm install prometheus prometheus-community/prometheus -f .\deployment\helm-prometheus-values.yaml -n observability

echo ================================================================
echo Install ArgoCD
echo ================================================================
kubectl create namespace argocd
helm install argocd argo/argo-cd -n argocd

echo ================================================================
echo Login into ArgoCD
echo ================================================================
rem Get initial admin password
argocd admin initial-password -n argocd --port-forward-namespace argocd
rem Port-forward ArgoCD server and login
kubectl port-forward service/argocd-server -n argocd 8080:443
rem Login to ArgoCD using CLI with the initial password and username "admin"
argocd login localhost:8080 --name local
argocd account update-password

echo ==================== Access ArgoCD UI ==========================
echo kubectl port-forward service/argocd-server -n argocd 8080:443
echo ================================================================

echo ================================================================
echo Install ArgoCD Application that contains all (Apps of App Pattern)
echo ================================================================
@rem Create namespace for application with istio ambient mode labels
kubectl create namespace nagp-ecom --labels istio.io/dataplane-mode=ambient
kubectl apply -f .\deployment\application.yaml
