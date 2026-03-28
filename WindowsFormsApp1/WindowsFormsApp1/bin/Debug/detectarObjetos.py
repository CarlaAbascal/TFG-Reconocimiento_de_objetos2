"""
detectarObjetos.py
------------------
Detección de objetos con YOLOv8 (ultralytics).
Envía el nombre del objeto detectado mediante MQTT.
El vídeo se recibe desde el servidor WebRTC convertido a MJPEG.
"""

import cv2
import time
import sys
import paho.mqtt.client as mqtt

# ---------------------------- MQTT ----------------------------
MQTT_BROKER = "127.0.0.1"
MQTT_PORT = 1883
MQTT_TOPIC = "objetos"

mqtt_client = mqtt.Client()
mqtt_client.connect(MQTT_BROKER, MQTT_PORT, 60)
print("[OK] Conectado al broker MQTT (objetos)")

sys.stdout.reconfigure(encoding='utf-8')

# ---------------------------- MODELO YOLOv8 ----------------------------
try:
    from ultralytics import YOLO
    model = YOLO("yolov8n.pt")  # modelo ligero
    print("[OK] Modelo YOLOv8 cargado correctamente.")
except Exception as e:
    print(f"[ERROR] No se pudo cargar el modelo YOLO: {e}")
    sys.exit(1)

# ---------------------------- INICIAR STREAM MJPEG ----------------------------
cap = cv2.VideoCapture("http://localhost:8080/stream.mjpg")
if not cap.isOpened():
    print("[ERROR] No se puede abrir el stream de vídeo.")
    sys.exit(1)

print("[OK] Stream de vídeo iniciado correctamente.")

# ---------------------------- CONTROL DE ENVÍO ----------------------------
ultimo_objeto = None
ultimo_tiempo = 0
DELAY = 1.0  # segundos entre envíos repetidos

# ---------------------------- BUCLE PRINCIPAL ----------------------------
while True:
    ret, frame = cap.read()
    if not ret:
        print("[WARN] No se pudo leer frame, reintentando...")
        time.sleep(0.1)
        continue

    results = model(frame, verbose=False)

    if len(results) > 0:
        for box in results[0].boxes:
            clase = int(box.cls[0])
            nombre = results[0].names.get(clase, "desconocido")
            conf = float(box.conf[0])

            if conf > 0.6:
                ahora = time.time()
                if nombre != ultimo_objeto or (ahora - ultimo_tiempo) > DELAY:
                    mqtt_client.publish(MQTT_TOPIC, nombre)
                    print(f"[OBJETO] Publicado por MQTT: {nombre}")
                    ultimo_objeto = nombre
                    ultimo_tiempo = ahora
                break

    # Debug opcional
    cv2.imshow("Objetos (debug local)", frame)
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
mqtt_client.disconnect()
cv2.destroyAllWindows()
print("[INFO] Script de objetos finalizado correctamente.")
