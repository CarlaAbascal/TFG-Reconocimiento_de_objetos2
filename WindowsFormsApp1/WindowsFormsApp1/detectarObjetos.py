"""
detectarObjetos.py
------------------
Detección de objetos con YOLOv8 (ultralytics).
Envía el nombre del objeto detectado y el vídeo al proyecto C# por sockets TCP.
"""

import cv2
import socket
import struct
import time
import sys

# ---------------------------- CONFIGURACIÓN TCP ----------------------------
HOST = "127.0.0.1"
PORT_DATA = 5007   # envío de texto (nombre del objeto)
PORT_VIDEO = 5006  # streaming de vídeo

sys.stdout.reconfigure(encoding='utf-8')

# ---------------------------- CONEXIONES ----------------------------
try:
    sock_data = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock_data.connect((HOST, PORT_DATA))
    print("[OK] Conectado al servidor de datos C#")
except Exception as e:
    print(f"[ERROR] No se pudo conectar al servidor de datos: {e}")
    sys.exit(1)

try:
    sock_video = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock_video.connect((HOST, PORT_VIDEO))
    print("[OK] Conectado al servidor de vídeo C#")
except Exception as e:
    print(f"[ERROR] No se pudo conectar al servidor de vídeo: {e}")
    sock_data.close()
    sys.exit(1)

# ---------------------------- MODELO YOLOv8 ----------------------------
try:
    from ultralytics import YOLO
    model = YOLO("yolov8n.pt")  # modelo ligero (se descarga solo una vez)
    print("[OK] Modelo YOLOv8 cargado correctamente.")
except Exception as e:
    print(f"[ERROR] No se pudo cargar el modelo YOLO: {e}")
    sock_data.close()
    sock_video.close()
    sys.exit(1)

# ---------------------------- INICIAR CÁMARA ----------------------------
cap = cv2.VideoCapture(0)
if not cap.isOpened():
    print("[ERROR] No se puede abrir la cámara.")
    sock_data.close()
    sock_video.close()
    sys.exit(1)

print("[OK] Cámara iniciada correctamente.")

# ---------------------------- CONTROL DE ENVÍO ----------------------------
ultimo_objeto = None
ultimo_tiempo = 0
DELAY = 1.0  # segundos entre envíos repetidos

# ---------------------------- BUCLE PRINCIPAL ----------------------------
while True:
    ret, frame = cap.read()
    if not ret:
        print("[ERROR] No se pudo leer el frame de la cámara.")
        break

    # Detección de objetos
    results = model(frame, verbose=False)
    if len(results) > 0:
        for box in results[0].boxes:
            clase = int(box.cls[0])
            nombre = results[0].names.get(clase, "desconocido")
            conf = float(box.conf[0])

            # solo si la confianza es alta
            if conf > 0.6:
                ahora = time.time()
                if nombre != ultimo_objeto or (ahora - ultimo_tiempo) > DELAY:
                    sock_data.sendall((nombre + "\n").encode("utf-8"))
                    print(f"[OBJETO] Enviado: {nombre}")
                    ultimo_objeto = nombre
                    ultimo_tiempo = ahora
                break

    # Enviar frame a C#
    try:
        _, buffer = cv2.imencode('.jpg', frame)
        data = buffer.tobytes()
        sock_video.sendall(struct.pack('>L', len(data)) + data)
    except (BrokenPipeError, ConnectionResetError):
        print("[ERROR] Conexión de vídeo cerrada por C#.")
        break

    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

# ---------------------------- FINALIZAR ----------------------------
cap.release()
sock_data.close()
sock_video.close()
print("[INFO] Script finalizado correctamente.")
