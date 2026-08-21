# S2 measurement deployment

Manifests for running the app on the real 8GB Pi cluster node (`pinode02`), to measure
classification ms/image, RAM, and filter rate under real load (see the S2 plan / PR #5).

`01-app.yaml` (PVC + Deployment + Service) is **Flux-managed**: the central `studylife` repo's
`k8s/flux/21-25-unifiprotectdashboard-*.yaml` watch GHCR for new image tags and auto-bump the
`$imagepolicy`-marked image line via `k8s/flux-deploy/kustomization.yaml`, same pattern as
`studylife-mcp`/`piwatch`. `00-namespace.yaml`, `02-httproute.yaml`, and `03-network-policies.yaml`
stay **bootstrap-only** - applied once by hand, never touched by Flux (kustomize-controller's
least-privilege ClusterRole doesn't grant those resource kinds - see
`k8s/flux-deploy/kustomization.yaml`).

## Bootstrap (once)

```bash
export KUBECONFIG=$env:USERPROFILE\.kube\studylife-config   # PowerShell
kubectl apply -f k8s/00-namespace.yaml
kubectl apply -f k8s/02-httproute.yaml
kubectl apply -f k8s/03-network-policies.yaml
```

After this, Flux's `unifiprotectdashboard-deploy` Kustomization applies `01-app.yaml` on its own
(5-minute reconcile interval) - no manual `kubectl apply -f k8s/01-app.yaml` needed afterwards.

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

Suspend Flux first, or it will simply recreate what you delete:

```bash
flux suspend kustomization unifiprotectdashboard-deploy   # in the studylife repo's cluster context
kubectl delete -f k8s/01-app.yaml
kubectl delete -f k8s/03-network-policies.yaml
kubectl delete -f k8s/02-httproute.yaml
kubectl delete -f k8s/00-namespace.yaml
```
