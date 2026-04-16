import cv2
import time
import paho.mqtt.client as mqtt
import mediapipe as mp
import os
import sys

print("SCRIPT EJECUTADO DESDE:", __file__)
print("=== SCRIPT GESTOS INICIADO ===")
print("Python:", sys.executable)
print("Working dir:", os.getcwd())

# ---------------- MQTT ----------------
MQTT_BROKER = "127.0.0.1"
MQTT_PORT = 1883
MQTT_TOPIC = "gestos"

print("[INFO] Creando cliente MQTT...")
mqtt_client = mqtt.Client()

print("[INFO] Conectando al broker MQTT...")
mqtt_client.connect(MQTT_BROKER, MQTT_PORT, 60)
mqtt_client.loop_start()
print("[OK] Conectado al broker MQTT (gestos)")

# ---------------- MediaPipe Hands ----------------
mp_hands = mp.solutions.hands
mp_draw = mp.solutions.drawing_utils
mp_styles = mp.solutions.drawing_styles

hands = mp_hands.Hands(
    static_image_mode=False,
    max_num_hands=1,
    min_detection_confidence=0.6,
    min_tracking_confidence=0.6
)

# ---------------- Stream ----------------
STREAM_URL = "http://localhost:8080/stream.mjpg"
print("[INFO] Abriendo stream:", STREAM_URL)

cap = cv2.VideoCapture(STREAM_URL)
if not cap.isOpened():
    print("[ERROR] No se puede abrir el stream de vídeo.")
    sys.exit(1)

print("[OK] Stream de vídeo iniciado.")

ultimo = None
ultimo_tiempo = 0
DELAY = 0.8

def contar_dedos(hand_landmarks, handedness_label, w, h):
    lm = hand_landmarks.landmark
    pts = [(int(p.x * w), int(p.y * h)) for p in lm]

    dedos = 0

    # Pulgar: depende de si es mano derecha o izquierda
    if handedness_label == "Right":
        if pts[4][0] < pts[3][0]:
            dedos += 1
    else:
        if pts[4][0] > pts[3][0]:
            dedos += 1

    # Índice, medio, anular, meñique:
    # dedo levantado si la punta está por encima del PIP
    for tip, pip in [(8, 6), (12, 10), (16, 14), (20, 18)]:
        if pts[tip][1] < pts[pip][1]:
            dedos += 1

    return dedos

while True:
    ret, frame = cap.read()
    if not ret:
        print("[WARN] Frame no recibido.")
        time.sleep(0.05)
        continue

    frame = cv2.flip(frame, 1)  # opcional, suele ir mejor visualmente
    rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    results = hands.process(rgb)

    gesto = None

    if results.multi_hand_landmarks and results.multi_handedness:
        hand_landmarks = results.multi_hand_landmarks[0]
        handedness_label = results.multi_handedness[0].classification[0].label

        mp_draw.draw_landmarks(
            frame,
            hand_landmarks,
            mp_hands.HAND_CONNECTIONS,
            mp_styles.get_default_hand_landmarks_style(),
            mp_styles.get_default_hand_connections_style()
        )

        h, w, _ = frame.shape
        dedos = contar_dedos(hand_landmarks, handedness_label, w, h)

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

        cv2.putText(
            frame,
            f"Gesto: {gesto}",
            (20, 40),
            cv2.FONT_HERSHEY_SIMPLEX,
            1,
            (0, 255, 0),
            2
        )

    if gesto is not None:
        ahora = time.time()
        if gesto != ultimo or (ahora - ultimo_tiempo) > DELAY:
            mqtt_client.publish(MQTT_TOPIC, gesto)
            print("[GESTO] Enviado:", gesto)
            ultimo = gesto
            ultimo_tiempo = ahora

    cv2.imshow("Detección de mano (MediaPipe)", frame)
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
hands.close()
cv2.destroyAllWindows()
mqtt_client.loop_stop()
mqtt_client.disconnect()
print("[INFO] Script de gestos finalizado correctamente.")