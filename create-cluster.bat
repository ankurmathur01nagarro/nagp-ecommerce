k3d cluster create local --agents 2 --port "80:80@loadbalancer" --port "443:443@loadbalancer" --port "8000:8000@loadbalancer" --k3s-arg "--disable=traefik@server:0"

echo ================================================================
echo Install Gateway API, ArgoCD CRDs
echo ================================================================
kubectl apply --server-side -f "https://github.com/kubernetes-sigs/gateway-api/releases/download/v1.4.1/standard-install.yaml"

helm repo add argo https://argoproj.github.io/argo-helm
helm repo add traefik https://traefik.github.io/charts
helm repo update

kubectl create namespace argocd
helm install argocd argo/argo-cd -n argocd

echo ================================================================
echo Install traefik
echo ================================================================
kubectl create namespace traefik
rem Generate a self signed certificate valid for *.docker.localhost
openssl req -x509 -nodes -days 365 -newkey rsa:2048 -keyout tls.key -out tls.crt -subj "/CN=*.docker.localhost"
rem Create the TLS secret in the traefik namespace
kubectl create secret tls local-selfsigned-tls --cert=tls.crt --key=tls.key --namespace traefik
helm install traefik traefik/traefik -n traefik --values .\deployment\traefik-values.yaml

echo ================================================================
echo Login into ArgoCD
echo ================================================================
rem Get initial admin password
argocd admin initial-password -n argocd --port-forward-namespace argocd
rem Port-forward ArgoCD server and login
kubectl port-forward service/argocd-server -n argocd 8080:443
argocd login localhost:8080 --name local
argocd account update-password --port-forward-namespace argocd
echo ==================== Access ArgoCD UI ==========================
echo kubectl port-forward service/argocd-server -n argocd 8080:443
echo ================================================================

echo ================================================================
echo Install ArgoCD Application that contains all (Apps of App Pattern)
echo ================================================================
kubectl create namespace nagp-ecom
kubectl apply -f .\deployment\application.yaml
