import cv2 as cv
import torch
import socket
import time
import sys

# --- CONFIGURACIÓN TCP ---
HOST = "127.0.0.1"
PORT = 5007
time.sleep(2)  # Espera para dar tiempo al servidor C#

try:
    client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    client.connect((HOST, PORT))
    print(f"✅ Conectado al servidor C# en {HOST}:{PORT}", flush=True)
except Exception as e:
    print(f"❌ Error al conectar con el servidor C#: {e}", flush=True)
    sys.exit(1)

# --- MODELO YOLOv5 ---
print("🧠 Cargando modelo YOLOv5s...", flush=True)
model = torch.hub.load('ultralytics/yolov5', 'yolov5s', pretrained=True)

BANANA_CLASS_ID = 46
CARROT_CLASS_ID = 51
DONUT_CLASS_ID = 54
FORK_CLASS_ID = 42
CLOCK_CLASS_ID = 74
PIZZA_CLASS_ID = 53

cap = cv.VideoCapture(0)
if not cap.isOpened():
    print("❌ No se pudo abrir la cámara.", flush=True)
    client.close()
    sys.exit(1)
else:
    print("✅ Cámara abierta correctamente.", flush=True)

while cap.isOpened():
    ret, frame = cap.read()
    if not ret:
        print("⚠️ No se pudo leer frame.", flush=True)
        break

    img_rgb = cv.cvtColor(frame, cv.COLOR_BGR2RGB)
    results = model(img_rgb)
    detected = None

    for *box, conf, cls in results.xyxy[0]:
        obj = int(cls.item())
        if obj == BANANA_CLASS_ID: detected = "BANANA"
        elif obj == CLOCK_CLASS_ID: detected = "RELOJ"
        elif obj == DONUT_CLASS_ID: detected = "DONUT"
        elif obj == CARROT_CLASS_ID: detected = "ZANAHORIA"
        elif obj == PIZZA_CLASS_ID: detected = "PIZZA"
        elif obj == FORK_CLASS_ID: detected = "TENEDOR"

        if detected:
            msg = detected + "\n"
            client.sendall(msg.encode("utf-8"))
            print(f"📤 Enviado: {detected}", flush=True)
            break

    cv.imshow("Detector de objetos", frame)
    if cv.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
client.close()
cv.destroyAllWindows()
print("✅ Script finalizado correctamente.", flush=True)
