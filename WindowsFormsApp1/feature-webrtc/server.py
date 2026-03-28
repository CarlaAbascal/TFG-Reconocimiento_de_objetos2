import asyncio
import json
import logging
from aiohttp import web

import cv2  # 🔹 nou
from aiortc import RTCPeerConnection, RTCSessionDescription, RTCConfiguration, RTCIceServer
from aiortc.contrib.media import MediaRelay

logging.basicConfig(level=logging.INFO)

pcs_publishers = set()
pcs_viewers = set()

relay = MediaRelay()
relay_video_track = None  # track que llega del publisher

# Configuración WebRTC con STUN
rtc_config = RTCConfiguration(
    iceServers=[
        RTCIceServer(urls="stun:stun.l.google.com:19302")
    ]
)


async def index(request):
    return web.FileResponse("client.html")


async def publish_offer(request):
    global relay_video_track

    params = await request.json()
    offer = RTCSessionDescription(sdp=params["sdp"], type=params["type"])

    pc = RTCPeerConnection(rtc_config)
    pcs_publishers.add(pc)
    logging.info("Publisher conectado: pc=%s", id(pc))

    @pc.on("track")
    def on_track(track):
        global relay_video_track
        logging.info("Track recibido del publisher: %s", track.kind)
        if track.kind == "video":
            relay_video_track = relay.subscribe(track)
            logging.info("Track de vídeo listo para viewers")

    await pc.setRemoteDescription(offer)
    answer = await pc.createAnswer()
    await pc.setLocalDescription(answer)

    return web.Response(
        content_type="application/json",
        text=json.dumps({"sdp": pc.localDescription.sdp, "type": pc.localDescription.type}),
    )


async def viewer_offer(request):
    global relay_video_track

    if relay_video_track is None:
        return web.Response(
            status=503,
            content_type="application/json",
            text=json.dumps({"error": "No hay vídeo disponible todavía. Arranca el publisher primero."}),
        )

    params = await request.json()
    offer = RTCSessionDescription(sdp=params["sdp"], type=params["type"])

    pc = RTCPeerConnection(rtc_config)
    pcs_viewers.add(pc)
    logging.info("Viewer conectado: pc=%s", id(pc))

    pc.addTrack(relay_video_track)

    await pc.setRemoteDescription(offer)
    answer = await pc.createAnswer()
    await pc.setLocalDescription(answer)

    return web.Response(
        content_type="application/json",
        text=json.dumps({"sdp": pc.localDescription.sdp, "type": pc.localDescription.type}),
    )


# Endpoint MJPEG perquè OpenCV el pugui llegir
async def stream_mjpeg(request):
    global relay_video_track

    if relay_video_track is None:
        return web.Response(
            status=503,
            text="No hi ha vídeo disponible encara. Arrenca el publisher."
        )

    resp = web.StreamResponse(
        status=200,
        headers={
            "Content-Type": "multipart/x-mixed-replace; boundary=frame"
        },
    )
    await resp.prepare(request)

    try:
        while True:
            # Rebre frame WebRTC (aiortc.VideoFrame)
            frame = await relay_video_track.recv()

            # Convertir a numpy (OpenCV)
            img = frame.to_ndarray(format="bgr24")

            # Codificar a JPEG
            ok, jpg = cv2.imencode(".jpg", img)
            if not ok:
                continue

            chunk = (
                b"--frame\r\n"
                b"Content-Type: image/jpeg\r\n\r\n" +
                jpg.tobytes() +
                b"\r\n"
            )
            await resp.write(chunk)

    except asyncio.CancelledError:
        pass
    except Exception as e:
        logging.error("Error en stream_mjpeg: %s", e)

    return resp



async def on_shutdown(app):
    coros = []
    for pc in list(pcs_publishers) + list(pcs_viewers):
        coros.append(pc.close())
    await asyncio.gather(*coros)
    pcs_publishers.clear()
    pcs_viewers.clear()


app = web.Application()
app.on_shutdown.append(on_shutdown)

app.router.add_get("/", index)
app.router.add_post("/publish_offer", publish_offer)
app.router.add_post("/viewer_offer", viewer_offer)
app.router.add_get("/stream.mjpg", stream_mjpeg)  # endpoint

for r in app.router.routes():
    print("ROUTE:", r.method, r.resource)

if __name__ == "__main__":
    web.run_app(app, port=8080)
