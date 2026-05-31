using csDronLink;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

// >>> WEBRTC
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;

// >>> MQTT
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        Dron miDron = new Dron();

        // >>> WEBRTC: viewer dentro del formulario
        private WebView2 webViewRTC;

        // >>> Procesos Python
        private Process webrtcServerProcess;
        private Process webrtcPublisherProcess;
        private Process procesoGestos;
        private Process procesoObjetos;

        // >>> MQTT
        private IMqttClient mqttClient;
        private bool mqttConnected = false;

        // Estados
        private bool modoGestosActivo = false;
        private bool modoObjetosActivo = false;

        // Evita iniciar dos veces server/publisher si se pulsa varias veces
        private bool webRtcIniciandose = false;

        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;

            // Crear WebView2 en el mismo sitio que pictureBoxPC
            webViewRTC = new WebView2();
            webViewRTC.Left = pictureBoxPC.Left;
            webViewRTC.Top = pictureBoxPC.Top;
            webViewRTC.Width = pictureBoxPC.Width;
            webViewRTC.Height = pictureBoxPC.Height;
            webViewRTC.Anchor = pictureBoxPC.Anchor;

            this.Controls.Add(webViewRTC);
            webViewRTC.BringToFront();

            this.Load += Form1_Load;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            IniciarMQTT();

            try
            {
                await webViewRTC.EnsureCoreWebView2Async();
                webViewRTC.Source = new Uri("about:blank");
            }
            catch (Exception ex)
            {
                AñadirLog("⚠️ Error inicializando WebView2: " + ex.Message);
            }
        }

        // =========================================================
        //                       RUTAS
        // =========================================================

        private string GetAppPath()
        {
            // ...\WindowsFormsApp1\WindowsFormsApp1\bin\Debug
            return Application.StartupPath;
        }

        private string GetProjectRoot()
        {
            // ...\TFG-Reconocimiento_de_objetos2\WindowsFormsApp1
            return Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\.."));
        }

        private string GetRepoRoot()
        {
            // ...\TFG-Reconocimiento_de_objetos2
            return Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\..\.."));
        }

        private string GetPythonGestos()
        {
            return Path.Combine(GetRepoRoot(), "gestos_env310", "Scripts", "python.exe");
        }

        private string GetPythonWebRtcObjetos()
        {
            return Path.Combine(GetRepoRoot(), "mp_env", "Scripts", "python.exe");
        }

        private string GetFeatureWebRtcPath()
        {
            return Path.Combine(GetProjectRoot(), "feature-webrtc");
        }

        private string GetServerPath()
        {
            return Path.Combine(GetFeatureWebRtcPath(), "server.py");
        }

        private string GetPublisherPath()
        {
            return Path.Combine(GetFeatureWebRtcPath(), "script_publisher.py");
        }

        private string GetGestosScriptPath()
        {
            return Path.Combine(GetAppPath(), "detectar_mano_mp.py");
        }

        private string GetObjetosScriptPath()
        {
            return Path.Combine(GetAppPath(), "detectarObjetos.py");
        }

        // =========================================================
        //                       LOG
        // =========================================================

        private void AñadirLog(string texto)
        {
            try
            {
                if (listBox1.InvokeRequired)
                {
                    listBox1.Invoke(new Action(() =>
                    {
                        listBox1.Items.Add(texto);
                        listBox1.TopIndex = listBox1.Items.Count - 1;
                    }));
                }
                else
                {
                    listBox1.Items.Add(texto);
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                }
            }
            catch
            {
                // Evita que un fallo de log rompa la aplicación
            }
        }

        private async Task<bool> EsperarVideoDisponible(int timeoutMs)
        {
            using (HttpClient client = new HttpClient())
            {
                DateTime inicio = DateTime.Now;

                while ((DateTime.Now - inicio).TotalMilliseconds < timeoutMs)
                {
                    try
                    {
                        string respuesta = await client.GetStringAsync("http://127.0.0.1:8080/status");

                        if (respuesta.Contains("\"video\": true") || respuesta.Contains("\"video\":true"))
                            return true;
                    }
                    catch
                    {
                        // El servidor encara no està llest
                    }

                    await Task.Delay(700);
                }
            }

            return false;
        }

        private bool OcultarLogPython(string tag, string linea)
        {
            if (string.IsNullOrWhiteSpace(linea))
                return true;

            // Logs molt repetitius del servidor
            if (tag == "SERVER")
            {
                if (linea.Contains("aiohttp.access"))
                    return true;

                if (linea.Contains("aioice.ice"))
                    return true;

                if (linea.Contains("Check CandidatePair"))
                    return true;

                if (linea.Contains("State.FROZEN"))
                    return true;

                if (linea.Contains("State.WAITING"))
                    return true;

                if (linea.Contains("State.IN_PROGRESS"))
                    return true;

                if (linea.Contains("State.SUCCEEDED"))
                    return true;

                if (linea.Contains("State.FAILED"))
                    return true;

                if (linea.Contains("ICE completed"))
                    return true;

                if (linea.StartsWith("ROUTE:"))
                    return true;

                if (linea.Contains("Cannot write to closing transport"))
                    return true;
            }

            // Logs molestos de MediaPipe/TensorFlow
            if (tag == "GESTOS")
            {
                if (linea.Contains("DeprecationWarning"))
                    return true;

                if (linea.Contains("TensorFlow Lite XNNPACK"))
                    return true;

                if (linea.Contains("WARNING: All log messages"))
                    return true;

                if (linea.Contains("inference_feedback_manager"))
                    return true;

                if (linea.Contains("landmark_projection_calculator"))
                    return true;

                if (linea.StartsWith("W0000"))
                    return true;
            }

            return false;
        }

        // =========================================================
        //                       TELEMETRÍA
        // =========================================================

        private void ProcesarTelemetria(byte id, List<(string nombre, float valor)> telemetria)
        {
            foreach (var t in telemetria)
            {
                if (t.nombre == "Alt")
                {
                    altLbl.Text = t.valor.ToString();
                    break;
                }
            }
        }

        // =========================================================
        //                       BOTÓN CONECTAR
        // =========================================================

        private async void button1_Click_1(object sender, EventArgs e)
        {
            try
            {
                AñadirLog("[INFO] Intentando conectar al dron en modo simulacion...");
                miDron.Conectar("simulacion");
                AñadirLog("[OK] Conexión solicitada al dron.");

                miDron.EnviarDatosTelemetria(ProcesarTelemetria);
                AñadirLog("[OK] Telemetría solicitada.");

                // AQUÍ arrancamos server.py y script_publisher.py
                AñadirLog("[INFO] Iniciando servidor WebRTC y publisher de vídeo...");
                bool webRtcOk = await IniciarServerYPublisher();

                if (webRtcOk)
                {
                    AñadirLog("[OK] Server y publisher preparados.");
                    webViewRTC.Source = new Uri("http://localhost:8080/");
                }
                else
                {
                    AñadirLog("[ERROR] No se pudo preparar server/publisher.");
                }
            }
            catch (Exception ex)
            {
                AñadirLog("[ERROR] Conectar: " + ex.Message);
            }
        }

        // =========================================================
        //                       BOTONES DRON
        // =========================================================

        private void EnAire(byte id, object param)
        {
            button2.BackColor = Color.Green;
            button2.ForeColor = Color.White;
            button2.Text = (string)param;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                miDron.Despegar(20, bloquear: false, f: EnAire, param: "Volando");
                button2.BackColor = Color.Yellow;
            }
            catch (Exception ex)
            {
                AñadirLog("[ERROR] Despegar: " + ex.Message);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                miDron.Aterrizar(bloquear: false);
            }
            catch (Exception ex)
            {
                AñadirLog("[ERROR] Aterrizar: " + ex.Message);
            }
        }

        // =========================================================
        //                       MQTT
        // =========================================================

        private async void IniciarMQTT()
        {
            try
            {
                var factory = new MqttFactory();
                mqttClient = factory.CreateMqttClient();

                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer("127.0.0.1", 1883)
                    .Build();

                mqttClient.UseConnectedHandler(async e =>
                {
                    mqttConnected = true;
                    AñadirLog("MQTT connectat al broker.");

                    await mqttClient.SubscribeAsync("gestos");
                    AñadirLog("Subscrita al tema 'gestos'.");

                    await mqttClient.SubscribeAsync("objetos");
                    AñadirLog("Subscrita al tema 'objetos'.");
                });

                mqttClient.UseApplicationMessageReceivedHandler(e =>
                {
                    string topic = e.ApplicationMessage.Topic;
                    string mensaje = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

                    if (topic == "gestos")
                    {
                        AñadirLog("Gesto rebut per MQTT: " + mensaje);
                        EjecutarAccionPorGesto(mensaje);
                    }
                    else if (topic == "objetos")
                    {
                        AñadirLog("Objeto detectado por MQTT: " + mensaje);
                    }
                });

                await mqttClient.ConnectAsync(options);
            }
            catch (Exception ex)
            {
                AñadirLog("❌ Error connectant MQTT: " + ex.Message);
            }
        }

        // =========================================================
        //              START PROCESS PYTHON
        // =========================================================

        private Process StartProcess(string pythonExe, string scriptPath, string tag, string workingDir)
        {
            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = "-u \"" + scriptPath + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDir
            };

            psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";

            var p = new Process();
            p.StartInfo = psi;
            p.EnableRaisingEvents = true;

            p.OutputDataReceived += (s, e) =>
            {
                if (!OcultarLogPython(tag, e.Data))
                    AñadirLog("[" + tag + "] " + e.Data);
            };

            p.ErrorDataReceived += (s, e) =>
            {
                if (!OcultarLogPython(tag, e.Data))
                    AñadirLog("⚠️ [" + tag + "] " + e.Data);
            };

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            return p;
        }

        private void DetenerProceso(ref Process proceso, string nombre)
        {
            try
            {
                if (proceso != null && !proceso.HasExited)
                {
                    proceso.Kill();
                    proceso.WaitForExit(1000);
                    proceso.Dispose();
                    proceso = null;
                    AñadirLog("[INFO] " + nombre + " detenido.");
                }
            }
            catch (Exception ex)
            {
                AñadirLog("[WARN] No se pudo detener " + nombre + ": " + ex.Message);
            }
        }

        // =========================================================
        //              COMPROBAR PUERTO Y URL
        // =========================================================

        private bool PuertoAbierto(string host, int puerto)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    var result = client.BeginConnect(host, puerto, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));

                    if (!success)
                        return false;

                    client.EndConnect(result);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> EsperarUrlOk(string url, int timeoutMs)
        {
            using (HttpClient client = new HttpClient())
            {
                DateTime inicio = DateTime.Now;

                while ((DateTime.Now - inicio).TotalMilliseconds < timeoutMs)
                {
                    try
                    {
                        using (HttpResponseMessage response = await client.GetAsync(
                            url,
                            HttpCompletionOption.ResponseHeadersRead
                        ))
                        {
                            if (response.IsSuccessStatusCode)
                                return true;
                        }
                    }
                    catch
                    {
                        // Todavía no está disponible
                    }

                    await Task.Delay(500);
                }
            }

            return false;
        }

        // =========================================================
        //          INICIAR SERVER + PUBLISHER DESDE CONECTAR
        // =========================================================

        private async Task<bool> IniciarServerYPublisher()
        {
            if (webRtcIniciandose)
            {
                AñadirLog("[INFO] WebRTC ya se está iniciando. Espera...");
                await Task.Delay(3000);
                return PuertoAbierto("127.0.0.1", 8080);
            }

            webRtcIniciandose = true;

            try
            {
                string pythonWebRtc = GetPythonWebRtcObjetos();
                string featurePath = GetFeatureWebRtcPath();
                string serverPath = GetServerPath();
                string publisherPath = GetPublisherPath();

                AñadirLog("[INFO] Repo root: " + GetRepoRoot());
                AñadirLog("[INFO] feature-webrtc: " + featurePath);

                if (!File.Exists(pythonWebRtc))
                {
                    AñadirLog("[ERROR] No existe python mp_env: " + pythonWebRtc);
                    return false;
                }

                if (!File.Exists(serverPath))
                {
                    AñadirLog("[ERROR] No existe server.py: " + serverPath);
                    return false;
                }

                if (!File.Exists(publisherPath))
                {
                    AñadirLog("[ERROR] No existe script_publisher.py: " + publisherPath);
                    return false;
                }

                // 1. SERVER.PY
                if (PuertoAbierto("127.0.0.1", 8080))
                {
                    AñadirLog("[OK] El puerto 8080 ya está activo. No se inicia otro server.py.");
                }
                else
                {
                    AñadirLog("[INFO] Iniciando server.py...");

                    webrtcServerProcess = StartProcess(
                        pythonWebRtc,
                        serverPath,
                        "SERVER",
                        featurePath
                    );

                    bool serverOk = false;

                    for (int i = 0; i < 20; i++)
                    {
                        if (PuertoAbierto("127.0.0.1", 8080))
                        {
                            serverOk = true;
                            break;
                        }

                        await Task.Delay(500);
                    }

                    if (!serverOk)
                    {
                        AñadirLog("[ERROR] server.py no ha abierto el puerto 8080.");
                        return false;
                    }

                    AñadirLog("[OK] server.py iniciado en puerto 8080.");
                }

                // 2. PUBLISHER.PY
                bool streamYaActivo = await EsperarVideoDisponible(1500);

                if (streamYaActivo)
                {
                    AñadirLog("[OK] Stream MJPEG ya disponible.");
                    return true;
                }

                if (webrtcPublisherProcess == null || webrtcPublisherProcess.HasExited)
                {
                    AñadirLog("[INFO] Iniciando script_publisher.py...");

                    webrtcPublisherProcess = StartProcess(
                        pythonWebRtc,
                        publisherPath,
                        "PUBLISHER",
                        featurePath
                    );
                }
                else
                {
                    AñadirLog("[OK] script_publisher.py ya estaba iniciado.");
                }

                bool streamOk = await EsperarVideoDisponible(30000);

                if (!streamOk)
                {
                    AñadirLog("[ERROR] El publisher no ha publicado vídeo dentro del tiempo esperado.");
                    AñadirLog("[INFO] Revisa si la cámara está ocupada o si script_publisher.py falla.");
                    return false;
                }

                AñadirLog("[OK] Stream WebRTC/MJPEG disponible.");
                return true;
            }
            catch (Exception ex)
            {
                AñadirLog("[ERROR] IniciarServerYPublisher: " + ex.Message);
                return false;
            }
            finally
            {
                webRtcIniciandose = false;
            }
        }

        // =========================================================
        //                       BOTÓN GESTOS
        // =========================================================

        private async void btnGestos_Click(object sender, EventArgs e)
        {
            try
            {
                DetenerProceso(ref procesoObjetos, "Script de objetos");
                DetenerProceso(ref procesoGestos, "Script de gestos");

                modoGestosActivo = true;
                modoObjetosActivo = false;

                AñadirLog("[INFO] Cargando reconocimiento de gestos...");

                // Ya NO iniciamos server.py ni publisher aquí.
                // Solo comprobamos que el stream exista.
                bool streamOk = await EsperarUrlOk("http://127.0.0.1:8080/stream.mjpg", 3000);

                if (!streamOk)
                {
                    AñadirLog("[ERROR] No hay vídeo en http://127.0.0.1:8080/stream.mjpg");
                    AñadirLog("[INFO] Pulsa primero el botón Conectar para iniciar server.py y script_publisher.py.");
                    return;
                }

                string pythonGestos = GetPythonGestos();
                string gestosPath = GetGestosScriptPath();
                string appPath = GetAppPath();

                if (!File.Exists(pythonGestos))
                {
                    AñadirLog("[ERROR] No existe python gestos: " + pythonGestos);
                    return;
                }

                if (!File.Exists(gestosPath))
                {
                    AñadirLog("[ERROR] No existe detectar_mano_mp.py: " + gestosPath);
                    return;
                }

                AñadirLog("[INFO] Iniciando detectar_mano_mp.py...");

                procesoGestos = StartProcess(
                    pythonGestos,
                    gestosPath,
                    "GESTOS",
                    appPath
                );

                bool gestosWebOk = await EsperarUrlOk("http://127.0.0.1:8090/", 12000);

                if (!gestosWebOk)
                {
                    AñadirLog("[ERROR] El servidor de gestos no responde en http://127.0.0.1:8090/");
                    AñadirLog("[INFO] Revisa el log de [GESTOS].");
                    return;
                }

                if (webViewRTC.CoreWebView2 == null)
                    await webViewRTC.EnsureCoreWebView2Async();

                webViewRTC.Source = new Uri("http://127.0.0.1:8090/");
                AñadirLog("[INFO] Mostrando vídeo de gestos en el formulario.");
            }
            catch (Exception ex)
            {
                AñadirLog("[ERROR] btnGestos_Click: " + ex.Message);
            }
        }

        // =========================================================
        //                       ACCIONES POR GESTO
        // =========================================================

        private void EjecutarAccionPorGesto(string gesto)
        {
            try
            {
                switch (gesto.ToLower())
                {
                    case "palm":
                        miDron.Despegar(20, bloquear: false, f: EnAire, param: "Volando");
                        break;

                    case "puño":
                        miDron.Aterrizar(bloquear: false);
                        break;

                    case "uno":
                        miDron.Mover("Forward", 10, bloquear: false);
                        break;

                    case "dos":
                        miDron.CambiarHeading(90, bloquear: false);
                        break;

                    case "tres":
                        miDron.CambiarHeading(270, bloquear: false);
                        break;

                    default:
                        AñadirLog("Gesto no reconocido: " + gesto);
                        break;
                }
            }
            catch (Exception ex)
            {
                AñadirLog("[ERROR] Acción por gesto '" + gesto + "': " + ex.Message);
            }
        }

        // =========================================================
        //                       BOTÓN OBJETOS
        // =========================================================

        private async void btnObjetos_Click(object sender, EventArgs e)
        {
            try
            {
                DetenerProceso(ref procesoGestos, "Script de gestos");
                DetenerProceso(ref procesoObjetos, "Script de objetos");

                modoGestosActivo = false;
                modoObjetosActivo = true;

                AñadirLog("[INFO] Cargando reconocimiento de objetos...");

                // Ya NO iniciamos server.py ni publisher aquí.
                // Solo comprobamos que el stream exista.
                bool streamOk = await EsperarUrlOk("http://127.0.0.1:8080/stream.mjpg", 3000);

                if (!streamOk)
                {
                    AñadirLog("[ERROR] No hay vídeo en http://127.0.0.1:8080/stream.mjpg");
                    AñadirLog("[INFO] Pulsa primero el botón Conectar para iniciar server.py y script_publisher.py.");
                    return;
                }

                string pythonObjetos = GetPythonWebRtcObjetos();
                string objetosPath = GetObjetosScriptPath();
                string appPath = GetAppPath();

                if (!File.Exists(pythonObjetos))
                {
                    AñadirLog("[ERROR] No existe python objetos/mp_env: " + pythonObjetos);
                    return;
                }

                if (!File.Exists(objetosPath))
                {
                    AñadirLog("[ERROR] No existe detectarObjetos.py: " + objetosPath);
                    return;
                }

                AñadirLog("[INFO] Iniciando detectarObjetos.py...");

                procesoObjetos = StartProcess(
                    pythonObjetos,
                    objetosPath,
                    "OBJETOS",
                    appPath
                );

                if (webViewRTC.CoreWebView2 == null)
                    await webViewRTC.EnsureCoreWebView2Async();

                webViewRTC.Source = new Uri("http://localhost:8080/");
                AñadirLog("[INFO] Mostrando vídeo base en el formulario.");
            }
            catch (Exception ex)
            {
                AñadirLog("[ERROR] btnObjetos_Click: " + ex.Message);
            }
        }

        // =========================================================
        //                       BOTÓN DETENER
        // =========================================================

        private void btnDetener_Click(object sender, EventArgs e)
        {
            try
            {
                DetenerProceso(ref procesoGestos, "Script de gestos");
                DetenerProceso(ref procesoObjetos, "Script de objetos");
                DetenerProceso(ref webrtcPublisherProcess, "script_publisher.py");
                DetenerProceso(ref webrtcServerProcess, "server.py");

                modoGestosActivo = false;
                modoObjetosActivo = false;

                webViewRTC.Source = new Uri("about:blank");

                AñadirLog("[INFO] Todos los scripts detenidos.");
            }
            catch (Exception ex)
            {
                AñadirLog("[ERROR] btnDetener_Click: " + ex.Message);
            }
        }

        // =========================================================
        //                       BOTÓN COPIAR LOG
        // =========================================================

        private void btnCopiarLog_Click(object sender, EventArgs e)
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                foreach (var item in listBox1.Items)
                    sb.AppendLine(item.ToString());

                Clipboard.SetText(sb.ToString());
                MessageBox.Show("Log copiat al porta-retalls.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error copiando log: " + ex.Message);
            }
        }

        // =========================================================
        //                       FORM CLOSING
        // =========================================================

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                if (mqttClient != null && mqttConnected)
                    mqttClient.DisconnectAsync().Wait();
            }
            catch { }

            DetenerProceso(ref procesoGestos, "Script de gestos");
            DetenerProceso(ref procesoObjetos, "Script de objetos");
            DetenerProceso(ref webrtcPublisherProcess, "script_publisher.py");
            DetenerProceso(ref webrtcServerProcess, "server.py");

            base.OnFormClosing(e);
        }
    }
}