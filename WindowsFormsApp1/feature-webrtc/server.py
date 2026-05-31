import asyncio
import json
import logging
from aiohttp import web

import cv2
from aiortc import RTCPeerConnection, RTCSessionDescription, RTCConfiguration, RTCIceServer
from aiortc.contrib.media import MediaRelay

# Logs més nets
logging.basicConfig(level=logging.WARNING)
logging.getLogger("aiohttp.access").setLevel(logging.WARNING)
logging.getLogger("aioice").setLevel(logging.WARNING)
logging.getLogger("aiortc").setLevel(logging.WARNING)

pcs_publishers = set()
pcs_viewers = set()

relay = MediaRelay()
source_video_track = None  # track original que llega del publisher

# Configuración WebRTC con STUN
rtc_config = RTCConfiguration(
    iceServers=[
        RTCIceServer(urls="stun:stun.l.google.com:19302")
    ]
)


async def index(request):
    return web.FileResponse("client.html")


async def publish_offer(request):
    global source_video_track

    params = await request.json()
    offer = RTCSessionDescription(sdp=params["sdp"], type=params["type"])

    pc = RTCPeerConnection(rtc_config)
    pcs_publishers.add(pc)

    @pc.on("track")
    def on_track(track):
        global source_video_track

        if track.kind == "video":
            source_video_track = track
            print("[SERVER] Track de vídeo recibido del publisher")

    await pc.setRemoteDescription(offer)
    answer = await pc.createAnswer()
    await pc.setLocalDescription(answer)

    return web.Response(
        content_type="application/json",
        text=json.dumps({
            "sdp": pc.localDescription.sdp,
            "type": pc.localDescription.type
        }),
    )


async def viewer_offer(request):
    global source_video_track

    if source_video_track is None:
        return web.Response(
            status=503,
            content_type="application/json",
            text=json.dumps({
                "error": "No hay vídeo disponible todavía. Arranca el publisher primero."
            }),
        )

    params = await request.json()
    offer = RTCSessionDescription(sdp=params["sdp"], type=params["type"])

    pc = RTCPeerConnection(rtc_config)
    pcs_viewers.add(pc)

    viewer_track = relay.subscribe(source_video_track)
    pc.addTrack(viewer_track)

    await pc.setRemoteDescription(offer)
    answer = await pc.createAnswer()
    await pc.setLocalDescription(answer)

    return web.Response(
        content_type="application/json",
        text=json.dumps({
            "sdp": pc.localDescription.sdp,
            "type": pc.localDescription.type
        }),
    )


# Endpoint MJPEG perquè OpenCV el pugui llegir
async def stream_mjpeg(request):
    global source_video_track

    if source_video_track is None:
        return web.Response(
            status=503,
            text="No hi ha vídeo disponible encara. Arrenca el publisher."
        )

    mjpeg_track = relay.subscribe(source_video_track)

    resp = web.StreamResponse(
        status=200,
        headers={
            "Content-Type": "multipart/x-mixed-replace; boundary=frame"
        },
    )

    await resp.prepare(request)

    try:
        while True:
            frame = await mjpeg_track.recv()
            img = frame.to_ndarray(format="bgr24")

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
    except Exception:
        # Evitem omplir el log amb "Cannot write to closing transport"
        pass

    return resp


# Endpoint lleuger per saber si el publisher ja ha enviat vídeo
async def status(request):
    global source_video_track

    return web.Response(
        content_type="application/json",
        text=json.dumps({
            "server": True,
            "video": source_video_track is not None
        })
    )


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
app.router.add_get("/stream.mjpg", stream_mjpeg)
app.router.add_get("/status", status)


if __name__ == "__main__":
    print("[SERVER] Iniciando servidor en http://127.0.0.1:8080")
    web.run_app(app, host="127.0.0.1", port=8080, access_log=None)