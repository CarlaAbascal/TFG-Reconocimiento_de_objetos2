import cv2
import time
import paho.mqtt.client as mqtt
import mediapipe as mp
import os
import sys
import asyncio
import threading
from aiohttp import web

print("SCRIPT EJECUTADO DESDE:", __file__)
print("=== SCRIPT GESTOS MEDIAPIPE INICIADO ===")
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

hands = mp_hands.Hands(
    static_image_mode=False,
    max_num_hands=1,
    min_detection_confidence=0.6,
    min_tracking_confidence=0.6
)

# ---------------- Stream entrada ----------------
STREAM_URL = "http://localhost:8080/stream.mjpg"
print("[INFO] Abriendo stream:", STREAM_URL)

cap = cv2.VideoCapture(STREAM_URL)
if not cap.isOpened():
    print("[ERROR] No se puede abrir el stream de vídeo.")
    sys.exit(1)

print("[OK] Stream de vídeo iniciado.")

# ---------------- Control de estabilidad del gesto ----------------
ultimo_publicado = None
ultimo_tiempo_publicado = 0

gesto_candidato = None
gesto_candidato_inicio = 0

TIEMPO_ESTABLE = 3.0      # segundos que debe mantenerse el gesto
REPETICION_MINIMA = 2.0   # evita reenviar el mismo gesto demasiado rápido

latest_jpeg = None


def contar_dedos(hand_landmarks, handedness_label, w, h):
    lm = hand_landmarks.landmark
    pts = [(int(p.x * w), int(p.y * h)) for p in lm]

    dedos = 0

    # Pulgar
    if handedness_label == "Right":
        if pts[4][0] < pts[3][0]:
            dedos += 1
    else:
        if pts[4][0] > pts[3][0]:
            dedos += 1

    # Índice, medio, anular, meñique
    for tip, pip in [(8, 6), (12, 10), (16, 14), (20, 18)]:
        if pts[tip][1] < pts[pip][1]:
            dedos += 1

    return dedos


# ---------------- Servidor HTTP/MJPEG ----------------
async def index(request):
    html = """
    <html>
    <head>
        <meta charset="utf-8">
        <title>Gestos</title>
        <style>
            html, body {
                margin: 0;
                padding: 0;
                background: black;
                width: 100%;
                height: 100%;
                overflow: hidden;
            }
            .wrap {
                width: 100vw;
                height: 100vh;
                display: flex;
                align-items: center;
                justify-content: center;
                background: black;
            }
            img {
                max-width: 100vw;
                max-height: 100vh;
                width: 100%;
                height: 100%;
                object-fit: contain;
                display: block;
            }
        </style>
    </head>
    <body>
        <div class="wrap">
            <img src="/gestos.mjpg" />
        </div>
    </body>
    </html>
    """
    return web.Response(text=html, content_type="text/html")


async def gestos_stream(request):
    global latest_jpeg

    resp = web.StreamResponse(
        status=200,
        headers={"Content-Type": "multipart/x-mixed-replace; boundary=frame"},
    )
    await resp.prepare(request)

    try:
        while True:
            if latest_jpeg is not None:
                chunk = (
                    b"--frame\r\n"
                    b"Content-Type: image/jpeg\r\n\r\n" +
                    latest_jpeg +
                    b"\r\n"
                )
                await resp.write(chunk)

            await asyncio.sleep(0.03)
    except asyncio.CancelledError:
        pass
    except Exception as e:
        print("[ERROR] stream gestos:", e)

    return resp


async def start_http_server():
    app = web.Application()
    app.router.add_get("/", index)
    app.router.add_get("/gestos.mjpg", gestos_stream)

    runner = web.AppRunner(app)
    await runner.setup()
    site = web.TCPSite(runner, "127.0.0.1", 8090)
    await site.start()

    print("[OK] Servidor MJPEG de gestos en http://127.0.0.1:8090/")
    print("[OK] Stream MJPEG en http://127.0.0.1:8090/gestos.mjpg")

    while True:
        await asyncio.sleep(3600)


def run_http_server():
    asyncio.run(start_http_server())


server_thread = threading.Thread(target=run_http_server, daemon=True)
server_thread.start()

# Pequeño margen para que el servidor arranque
time.sleep(1.0)

# ---------------- Bucle principal ----------------
try:
    while True:
        ret, frame = cap.read()
        if not ret:
            print("[WARN] Frame no recibido.")
            time.sleep(0.05)
            continue

        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        results = hands.process(rgb)

        gesto = None
        tiempo_mantenido = 0.0

        if results.multi_hand_landmarks and results.multi_handedness:
            hand_landmarks = results.multi_hand_landmarks[0]
            handedness_label = results.multi_handedness[0].classification[0].label

            mp_draw.draw_landmarks(
                frame,
                hand_landmarks,
                mp_hands.HAND_CONNECTIONS
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

        ahora = time.time()

        if gesto is None:
            gesto_candidato = None
            gesto_candidato_inicio = 0

            cv2.putText(
                frame,
                "Estable: 0.0s",
                (20, 80),
                cv2.FONT_HERSHEY_SIMPLEX,
                0.8,
                (0, 255, 255),
                2
            )
        else:
            if gesto != gesto_candidato:
                gesto_candidato = gesto
                gesto_candidato_inicio = ahora

            tiempo_mantenido = ahora - gesto_candidato_inicio

            cv2.putText(
                frame,
                f"Estable: {tiempo_mantenido:.1f}s / {TIEMPO_ESTABLE:.1f}s",
                (20, 80),
                cv2.FONT_HERSHEY_SIMPLEX,
                0.8,
                (0, 255, 255),
                2
            )

            if tiempo_mantenido >= TIEMPO_ESTABLE:
                if gesto != ultimo_publicado or (ahora - ultimo_tiempo_publicado) >= REPETICION_MINIMA:
                    mqtt_client.publish(MQTT_TOPIC, gesto)
                    print(f"[GESTO] Enviado tras {TIEMPO_ESTABLE:.1f}s: {gesto}")

                    ultimo_publicado = gesto
                    ultimo_tiempo_publicado = ahora

                    # Reiniciar para evitar disparos continuos cada frame
                    gesto_candidato = None
                    gesto_candidato_inicio = 0

        ok, jpg = cv2.imencode(".jpg", frame)
        if ok:
            latest_jpeg = jpg.tobytes()

except KeyboardInterrupt:
    print("[INFO] Script interrumpido por el usuario.")

finally:
    try:
        cap.release()
    except:
        pass

    try:
        hands.close()
    except:
        pass

    try:
        mqtt_client.loop_stop()
    except:
        pass

    try:
        mqtt_client.disconnect()
    except:
        pass

    print("[INFO] Script de gestos finalizado correctamente.")