import cv2 as cv
import torch
import socket
import struct
import pickle
import time
import sys

# --- CONFIGURACIÓN TCP ---
HOST = "127.0.0.1"
PORT_DATA = 5007        # mensajes (objetos detectados)
PORT_VIDEO = 5006       # streaming de vídeo (igual que el de gestos)

sys.stdout.reconfigure(encoding='utf-8')
time.sleep(2)

# --- CONEXIÓN CON C# (mensajes) ---
try:
    client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    client.connect((HOST, PORT_DATA))
    print(f"Conectado al servidor C# en {HOST}:{PORT_DATA}", flush=True)
except Exception as e:
    print(f"Error al conectar con el servidor C#: {e}", flush=True)
    sys.exit(1)

# --- CONEXIÓN CON C# (vídeo) ---
try:
    video_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    video_socket.connect((HOST, PORT_VIDEO))
    print(f"Conectado al servidor de vídeo en puerto {PORT_VIDEO}", flush=True)
except Exception as e:
    print(f"Error al conectar con el servidor de vídeo: {e}", flush=True)
    client.close()
    sys.exit(1)

# --- CARGA DEL MODELO YOLO ---
try:
    print("Cargando modelo YOLOv5s...", flush=True)
    model = torch.hub.load('ultralytics/yolov5', 'yolov5s', pretrained=True)
except Exception as e:
    print(f"Error al cargar YOLOv5: {e}", flush=True)
    client.close()
    video_socket.close()
    sys.exit(1)

# --- ABRIR CÁMARA ---
cap = cv.VideoCapture(0)
if not cap.isOpened():
    print("No se pudo abrir la cámara.", flush=True)
    client.close()
    video_socket.close()
    sys.exit(1)
else:
    print("Cámara abierta correctamente.", flush=True)

# --- IDS DE OBJETOS ---
BANANA_CLASS_ID = 46
CARROT_CLASS_ID = 51
DONUT_CLASS_ID = 54
FORK_CLASS_ID = 42
CLOCK_CLASS_ID = 74
PIZZA_CLASS_ID = 53

# --- BUCLE PRINCIPAL ---
while cap.isOpened():
    ret, frame = cap.read()
    if not ret:
        print("No se pudo leer frame de la cámara.", flush=True)
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
            print(f"Enviado: {detected}", flush=True)
            break

    # --- Enviar frame comprimido al servidor C# ---
    try:
        _, encoded = cv.imencode('.jpg', frame)
        data = pickle.dumps(encoded, protocol=pickle.HIGHEST_PROTOCOL)
        size = struct.pack(">L", len(data))
        video_socket.sendall(size + data)
    except Exception as e:
        print(f"Error enviando frame: {e}", flush=True)
        break

    if cv.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
client.close()
video_socket.close()
cv.destroyAllWindows()
print("Script finalizado correctamente.", flush=True)
