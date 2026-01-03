k3d cluster create local --agents 2 --port "80:80@loadbalancer"
kubectl create namespace argocd
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml

kubectl port-forward service/argocd-server -n argocd 8080:443
argocd login localhost:8080 --name local

argocd admin initial-password -n argocd --port-forward-namespace argocd
argocd login localargo --port-forward-namespace argocd
argocd account update-password --port-forward-namespace argocd

helm upgrade --install nagp-ecom deployments\helm --namespace nagp-ecom

echo ==================== Access ArgoCD UI ==========================
echo kubectl port-forward service/argocd-server -n argocd 8080:443
echo ================================================================
