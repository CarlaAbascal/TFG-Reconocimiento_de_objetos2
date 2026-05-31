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
import warnings
import paho.mqtt.client as mqtt

# Evitar warnings molestos en consola
warnings.filterwarnings("ignore", category=DeprecationWarning)

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass


# ---------------------------- CONFIGURACIÓN ----------------------------

STREAM_URL = "http://localhost:8080/stream.mjpg"

MQTT_BROKER = "127.0.0.1"
MQTT_PORT = 1883
MQTT_TOPIC = "objetos"

MODELO_YOLO = "yolov8n.pt"

# Confianza mínima para aceptar una detección
CONF_MINIMA = 0.60

# Tiempo que debe mantenerse el objeto antes de publicarlo
TIEMPO_ESTABLE_OBJETO = 1.0

# Tiempo mínimo antes de volver a publicar el mismo objeto
REPETICION_MINIMA_OBJETO = 4.0

# Procesar 1 de cada N frames. Subirlo reduce carga y delay.
# 1 = procesa todos los frames
# 2 = procesa uno sí y uno no
SALTAR_FRAMES = 2

# Si quieres ignorar alguna clase, añádela aquí.
# Por ejemplo, para ignorar personas:
# IGNORAR_CLASES = {"person"}
IGNORAR_CLASES = set()

# Si quieres detectar solo algunos objetos concretos, ponlos aquí.
# Por ejemplo:
# SOLO_CLASES = {"person", "bottle", "cell phone", "chair"}
# Si está vacío, acepta cualquier clase.
SOLO_CLASES = set()


# ---------------------------- MQTT ----------------------------

try:
    try:
        mqtt_client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
    except Exception:
        mqtt_client = mqtt.Client()

    mqtt_client.connect(MQTT_BROKER, MQTT_PORT, 60)
    mqtt_client.loop_start()
    print("[OK] Conectado al broker MQTT (objetos)")
except Exception as e:
    print(f"[ERROR] No se pudo conectar al broker MQTT: {e}")
    sys.exit(1)


# ---------------------------- MODELO YOLOv8 ----------------------------

try:
    from ultralytics import YOLO

    model = YOLO(MODELO_YOLO)
    print("[OK] Modelo YOLOv8 cargado correctamente.")
except Exception as e:
    print(f"[ERROR] No se pudo cargar el modelo YOLO: {e}")
    sys.exit(1)


# ---------------------------- INICIAR STREAM MJPEG ----------------------------

cap = cv2.VideoCapture(STREAM_URL)
cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)

if not cap.isOpened():
    print("[ERROR] No se puede abrir el stream de vídeo.")
    sys.exit(1)

print("[OK] Stream de vídeo iniciado correctamente.")


# ---------------------------- CONTROL DE PUBLICACIÓN ----------------------------

ultimo_publicado = None
ultimo_tiempo_publicado = 0.0

objeto_candidato = None
objeto_candidato_inicio = 0.0

contador_frames = 0


def objeto_permitido(nombre):
    """
    Devuelve True si el objeto debe ser tenido en cuenta.
    """
    if nombre in IGNORAR_CLASES:
        return False

    if len(SOLO_CLASES) > 0 and nombre not in SOLO_CLASES:
        return False

    return True


def publicar_objeto(nombre):
    """
    Publica el objeto por MQTT.
    """
    mqtt_client.publish(MQTT_TOPIC, nombre)
    print(f"[OBJETO] Publicado por MQTT: {nombre}")


# ---------------------------- BUCLE PRINCIPAL ----------------------------

try:
    while True:
        ret, frame = cap.read()

        if not ret:
            time.sleep(0.05)
            continue

        contador_frames += 1

        if contador_frames % SALTAR_FRAMES != 0:
            continue

        # Ejecutar YOLO
        results = model(frame, verbose=False)

        mejor_objeto = None
        mejor_confianza = 0.0

        if len(results) > 0:
            r = results[0]

            if r.boxes is not None and len(r.boxes) > 0:
                for box in r.boxes:
                    clase = int(box.cls[0])
                    nombre = r.names.get(clase, "desconocido")
                    conf = float(box.conf[0])

                    if conf < CONF_MINIMA:
                        continue

                    if not objeto_permitido(nombre):
                        continue

                    # Nos quedamos con el objeto más fiable del frame
                    if conf > mejor_confianza:
                        mejor_confianza = conf
                        mejor_objeto = nombre

        ahora = time.time()

        # Si no hay objeto claro, reiniciamos candidato
        if mejor_objeto is None:
            objeto_candidato = None
            objeto_candidato_inicio = 0.0
            continue

        # Si cambia el candidato, empezamos a contar estabilidad
        if mejor_objeto != objeto_candidato:
            objeto_candidato = mejor_objeto
            objeto_candidato_inicio = ahora
            continue

        tiempo_estable = ahora - objeto_candidato_inicio

        # Publicar solo si el objeto se mantiene estable
        if tiempo_estable >= TIEMPO_ESTABLE_OBJETO:
            puede_publicar = (
                mejor_objeto != ultimo_publicado or
                (ahora - ultimo_tiempo_publicado) >= REPETICION_MINIMA_OBJETO
            )

            if puede_publicar:
                publicar_objeto(mejor_objeto)

                ultimo_publicado = mejor_objeto
                ultimo_tiempo_publicado = ahora

                # Reiniciamos candidato para evitar publicar en bucle continuo
                objeto_candidato = None
                objeto_candidato_inicio = 0.0

except KeyboardInterrupt:
    print("[INFO] Script de objetos interrumpido por el usuario.")

finally:
    try:
        cap.release()
    except Exception:
        pass

    try:
        mqtt_client.loop_stop()
        mqtt_client.disconnect()
    except Exception:
        pass

    print("[INFO] Script de objetos finalizado correctamente.")