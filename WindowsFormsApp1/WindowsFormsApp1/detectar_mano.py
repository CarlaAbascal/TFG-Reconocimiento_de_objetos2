"""
detectar_mano.py
----------------
Script para detectar gestos de la mano usando MediaPipe y enviar el resultado
a la aplicación C# (Form1.cs) mediante sockets TCP.

GESTOS:
  Puño → Aterrizar
  Un dedo → Avanzar
  Dos dedos → Girar derecha
  Tres dedos → Girar izquierda
  Palma → Despegar
"""

import cv2
import socket
import time

# ---------------------------- INTENTAR IMPORTAR MEDIAPIPE ----------------------------
try:
    import mediapipe as mp
except ImportError:
    print("[ERROR] No se encuentra la librería 'mediapipe'.")
    print("👉 Instálala con:")
    print("   python -m pip install mediapipe==0.10.14")
    exit()

# ---------------------------- CONFIGURACIÓN DEL SOCKET ----------------------------
TCP_IP = '127.0.0.1'   # Dirección local (localhost)
TCP_PORT = 5005        # Puerto de comunicación (debe coincidir con el de C#)

sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
print("[INFO] Conectando con el servidor C#...")
sock.connect((TCP_IP, TCP_PORT))
print("[OK] Conectado con la aplicación C#")

# ---------------------------- SOCKET DE VIDEO ----------------------------
VIDEO_IP = "127.0.0.1"
VIDEO_PORT = 5006

video_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
video_socket.connect((VIDEO_IP, VIDEO_PORT))

# ---------------------------- CONFIGURACIÓN DE MEDIAPIPE ----------------------------
# En versiones nuevas, las soluciones están en mp.solutions.hands.Hands
mp_hands = mp.solutions.hands
mp_drawing = mp.solutions.drawing_utils
mp_styles = mp.solutions.drawing_styles  # 🔹 estilos más modernos (opcional)

hands = mp_hands.Hands(
    model_complexity=0,
    static_image_mode=False,
    max_num_hands=1,
    min_detection_confidence=0.7,
    min_tracking_confidence=0.5
)

# ---------------------------- INICIAR CÁMARA ----------------------------
cap = cv2.VideoCapture(0)
cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)

if not cap.isOpened():
    print("[ERROR] No se puede acceder a la cámara.")
    exit()

# ---------------------------- FUNCIÓN DE DETECCIÓN DE GESTOS ----------------------------
def detectar_gesto(hand_landmarks):
    """
    Determina el gesto según qué dedos están extendidos.
    Devuelve un string con el nombre del gesto.
    """
    tips = [4, 8, 12, 16, 20]
    dedos = []

    # --- Pulgar ---
    # Comparamos la coordenada X con su articulación anterior.
    if hand_landmarks.landmark[tips[0]].x < hand_landmarks.landmark[tips[0] - 1].x:
        dedos.append(1)
    else:
        dedos.append(0)

    # --- Otros 4 dedos ---
    # Si la punta del dedo (tip) está por encima (menor Y) que la articulación intermedia (PIP)
    for id in range(1, 5):
        if hand_landmarks.landmark[tips[id]].y < hand_landmarks.landmark[tips[id] - 2].y:
            dedos.append(1)
        else:
            dedos.append(0)

    total_dedos = dedos.count(1)

    if total_dedos == 0:
        return "puño"
    elif total_dedos == 1:
        return "uno"
    elif total_dedos == 2:
        return "dos"
    elif total_dedos == 3:
        return "tres"
    elif total_dedos >= 4:
        return "palm"
    else:
        return None

# ---------------------------- CONTROL DE ENVÍO ----------------------------
ultimo_gesto = None
ultimo_tiempo = 0
DELAY_GESTO = 0.8
sock.setblocking(False)

# ---------------------------- BUCLE PRINCIPAL ----------------------------
while True:
    success, frame = cap.read()
    if not success:
        print("[ERROR] No se pudo leer frame de la cámara.")
        break

    frame_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    results = hands.process(frame_rgb)

    if results.multi_hand_landmarks:
        for hand_landmarks in results.multi_hand_landmarks:
            # Dibujo de la mano con estilo actualizado
            mp_drawing.draw_landmarks(
                frame,
                hand_landmarks,
                mp_hands.HAND_CONNECTIONS,
                mp_styles.get_default_hand_landmarks_style(),
                mp_styles.get_default_hand_connections_style()
            )

            gesto_detectado = detectar_gesto(hand_landmarks)

            ahora = time.time()
            if gesto_detectado:
                if gesto_detectado == ultimo_gesto:
                    if ahora - ultimo_tiempo > DELAY_GESTO:
                        try:
                            sock.sendall((gesto_detectado + "\n").encode('utf-8'))
                            print(f"[GESTO] Enviado: {gesto_detectado}")
                        except BlockingIOError:
                            pass
                        except Exception as e:
                            print(f"[ERROR] No se pudo enviar el gesto: {e}")
                        ultimo_tiempo = ahora
                else:
                    ultimo_gesto = gesto_detectado
                    ultimo_tiempo = ahora

    # 🔹 Enviar frame siempre (aunque no haya mano)
    try:
        _, buffer = cv2.imencode('.jpg', frame)
        data = buffer.tobytes()
        video_socket.sendall(len(data).to_bytes(4, byteorder='big'))
        video_socket.sendall(data)
    except (BrokenPipeError, ConnectionResetError):
        print("[ERROR] Conexión de video cerrada por C#.")
        break

# ---------------------------- FINALIZAR ----------------------------
cap.release()
sock.close()
video_socket.close()
hands.close()
print("[INFO] Conexión cerrada correctamente.")
