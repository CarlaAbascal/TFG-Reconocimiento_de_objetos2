import cv2
import time
import paho.mqtt.client as mqtt
from ultralytics import YOLO

# ---------------------------- MQTT ----------------------------
MQTT_BROKER = "127.0.0.1"
MQTT_PORT = 1883
MQTT_TOPIC = "gestos"

mqtt_client = mqtt.Client()
mqtt_client.connect(MQTT_BROKER, MQTT_PORT, 60)
print("[OK] Conectado al broker MQTT (gestos)")

# ---------------------------- YOLO (detección de manos) ----------------------------
model = YOLO("hand_yolov8n.pt")   # modelo de manos real
print("[OK] Modelo YOLO de manos cargado.")

# ---------------------------- STREAM MJPEG ----------------------------
cap = cv2.VideoCapture("http://localhost:8080/stream.mjpg")
if not cap.isOpened():
    print("[ERROR] No se puede abrir el stream de vídeo.")
    exit()

print("[OK] Stream de vídeo iniciado.")

ultimo = None
ultimo_tiempo = 0
DELAY = 0.8

def contar_dedos(keypoints):
    dedos = 0
    base = keypoints[0][1]  # muñeca

    # puntos de los dedos (modelo de 21 keypoints)
    dedos_indices = [4, 8, 12, 16, 20]

    for i in dedos_indices:
        if keypoints[i][1] < base:
            dedos += 1

    return dedos

while True:
    ret, frame = cap.read()
    if not ret:
        continue

    results = model(frame, verbose=False)

    if results and len(results[0].keypoints) > 0:
        kps = results[0].keypoints[0].xy.tolist()

        if len(kps) < 21:
            continue  # evitar errores

        dedos = contar_dedos(kps)

        if dedos == 0:
            gesto = "puño"
        elif dedos == 1:
            gesto = "uno"
        elif dedos == 2:
            gesto = "dos"
        elif dedos == 3:
            gesto = "tres"
        else:
            gesto = "palm"

        ahora = time.time()
        if gesto != ultimo or (ahora - ultimo_tiempo) > DELAY:
            mqtt_client.publish(MQTT_TOPIC, gesto)
            print("[GESTO] Enviado:", gesto)
            ultimo = gesto
            ultimo_tiempo = ahora

    cv2.imshow("Gestos YOLO", frame)
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
mqtt_client.disconnect()
cv2.destroyAllWindows()
print("[INFO] Script finalizado.")
