import asyncio
import cv2
import aiohttp
from av import VideoFrame
from aiortc import RTCPeerConnection, RTCSessionDescription, VideoStreamTrack, RTCConfiguration, RTCIceServer

SERVER_URL = "http://localhost:8080"

# Configuración WebRTC con STUN
rtc_config = RTCConfiguration(
    iceServers=[
        RTCIceServer(urls="stun:stun.l.google.com:19302")
    ]
)


class CameraStream(VideoStreamTrack):
    def __init__(self, camera_index=0):
        super().__init__()
        self.cap = cv2.VideoCapture(camera_index)

    async def recv(self):
        pts, time_base = await self.next_timestamp()

        ret, frame = self.cap.read()
        if not ret:
            await asyncio.sleep(0.1)
            return await self.recv()

        frame = cv2.flip(frame, 1)
        video_frame = VideoFrame.from_ndarray(frame, format="bgr24")
        video_frame.pts = pts
        video_frame.time_base = time_base
        return video_frame

    def close(self):
        try:
            if self.cap is not None:
                self.cap.release()
        except:
            pass


async def run_publisher():
    pc = RTCPeerConnection(rtc_config)

    camera = CameraStream(camera_index=0)
    pc.addTrack(camera)

    offer = await pc.createOffer()
    await pc.setLocalDescription(offer)

    async with aiohttp.ClientSession() as session:
        async with session.post(
            f"{SERVER_URL}/publish_offer",
            json={"sdp": pc.localDescription.sdp, "type": pc.localDescription.type},
        ) as resp:

            if resp.status != 200:
                text = await resp.text()
                print(f"Error al enviar offer: {resp.status} {text}")
                return

            answer = await resp.json()

    await pc.setRemoteDescription(
        RTCSessionDescription(sdp=answer["sdp"], type=answer["type"])
    )

    print("Publisher conectado. Enviando vídeo al servidor...")

    try:
        while True:
            await asyncio.sleep(1)
    except KeyboardInterrupt:
        print("Detenido por el usuario")
    finally:
        camera.close()
        await pc.close()


if __name__ == "__main__":
    asyncio.run(run_publisher())
