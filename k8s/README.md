# S2 measurement deployment

Temporary, hand-applied manifests for running the app on the real 8GB Pi cluster node
(`pinode02`) for about a week, to measure classification ms/image, RAM, and filter rate under
real load (see the S2 plan / PR #5). **Not** wired into the central `studylife` repo's Flux
GitOps pattern - this is deliberately not a permanent production onboarding, just applied by hand
for the measurement period.

## Apply

```bash
export KUBECONFIG=$env:USERPROFILE\.kube\studylife-config   # PowerShell
kubectl apply -f k8s/00-namespace.yaml
kubectl apply -f k8s/01-app.yaml
```

## First-time setup

No Kubernetes Secret for Protect credentials - the app stores them itself, AES-encrypted in its
own SQLite database, entered via the Setup wizard:

```bash
kubectl -n unifiprotectdashboard port-forward svc/unifiprotectdashboard 5003:5003
```

Open `http://localhost:5003/setup` and complete it once. State persists on the PVC.

## Watch it

```bash
kubectl -n unifiprotectdashboard logs -f deploy/unifiprotectdashboard | grep "YOLO classify"
```

## Tear down

```bash
kubectl delete -f k8s/01-app.yaml
kubectl delete -f k8s/00-namespace.yaml
```
