# YOLO model for event classification

This directory holds `yolo11n.onnx`, used to classify saved event thumbnails (person, car, ...).

The Docker image downloads it automatically at build time from this repo's `models` GitHub
Release - no manual setup is needed when running in a container.

## Local Development

The dashboard looks for the model at `Tools/yolo/yolo11n.onnx` (working directory or
`AppContext.BaseDirectory`).

### Manual Installation

Download it from this repo's release assets:

```powershell
Invoke-WebRequest -Uri "https://github.com/lukislp/UnifiProtectDashboard/releases/download/models/yolo11n.onnx" -OutFile "Tools\yolo\yolo11n.onnx"
```

Or export it yourself from [Ultralytics YOLO11](https://docs.ultralytics.com/models/yolo11/):

```bash
pip install ultralytics
python -c "from ultralytics import YOLO; YOLO('yolo11n.pt').export(format='onnx', imgsz=640, opset=17, simplify=True)"
```

## License

YOLO11 is licensed under **AGPL-3.0** by Ultralytics: https://github.com/ultralytics/ultralytics/blob/main/LICENSE

This binary is not committed to the repository (see `.gitignore`) - each developer downloads it
locally, and the Docker image downloads it at build time from this repo's `models` release.
