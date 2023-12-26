using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DunGen;
using GameNetcodeStuff;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.PlayerLoop;
using static System.Collections.Specialized.BitVector32;
using static System.Net.WebRequestMethods;
using Dissonance;
using Dissonance.Audio.Playback;
using Dissonance.Audio.Capture;
using NAudio.Wave;
using UnityEngine.Rendering;
using Microsoft.SqlServer.Server;
using Dissonance.Integrations.Unity_NFGO;
using System.Collections;
using Dissonance.Config;

namespace ParanoiaMod
{

    [BepInPlugin(modGUID, modName, modVersion)]
    public class Plugin : BaseUnityPlugin 
    {
        private const string modGUID = "Bestora.ParanoiaMod";
        private const string modName = "Paranoia Mod";
        private const string modVersion = "0.0.1";

        private readonly Harmony harmony = new Harmony(modGUID);

        public static Plugin Instance { get; private set; }

        public ManualLogSource Logger;

        private void Awake() 
        { 

            if(Instance == null)
            {
                Instance = this;
            }


            harmony.PatchAll(typeof(Plugin));


            Logger = BepInEx.Logging.Logger.CreateLogSource(modGUID);
            Plugin.Instance.Logger.LogInfo(" - - - - Init2 - - - - ");

            ParanoiaModConfig.InitConfig();

            GameObject gameObject = new GameObject(modName);
            gameObject.AddComponent<ParanoiaModPersistent>();
            gameObject.hideFlags = (HideFlags)61;
            DontDestroyOnLoad(gameObject);
        }

    }

    internal class ParanoiaModConfig
    {
        public static ConfigEntry<float> configMaxDistance;
        public static ConfigEntry<float> configMinWaitToPlayNext;
        public static ConfigEntry<float> configMaxWaitToPlayNext;


        public static void InitConfig()
        {
            configMaxDistance = Plugin.Instance.Config.Bind(
                "General",
                "MaxDistance",
                100f,
                "Max Distance to play audsoundio"
            );

            configMinWaitToPlayNext = Plugin.Instance.Config.Bind(
                "General",
                "MinWaitToPlayNext",
                10f,
                "Min seconds to play next sound"
            );


            configMaxWaitToPlayNext = Plugin.Instance.Config.Bind(
                "General",
                "MaxWaitToPlayNext",
                30f,
                "Max seconds to play next sound"
            );


        }

    }

    internal class ParanoiaModBehaviour : MonoBehaviour
    {
        private EnemyAI enemyAI;

        public void Initialize(EnemyAI enemyAI)
        {
            this.enemyAI = enemyAI;
        }

        private void Update()
        {
            // Enemy is dead
            if(enemyAI.isEnemyDead)
            {
                return;
            }


            Component player = GameNetworkManager.Instance.localPlayerController;
            Component ai = this;
            float distance = Vector3.Distance(player.transform.position, this.transform.position);

            // Not in distance
            if(distance >= ParanoiaModConfig.configMaxDistance.Value)
            {
                return;
            }

            
        }
    }

    internal class ParanoiaModPersistent : MonoBehaviour
    {
        public static ParanoiaModPersistent Instance { get; private set; }

        public PlayerVoiceIngameSettings playerVoiceIngameSettings;


        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

        }

        private void Update()
        {

            //VoicePlayback playbackComponent = GetComponent<VoicePlayback>();
            
            //DissonanceComms comms = StartOfRound.Instance.voiceChatModule;


            //foreach (PlayerControllerB playerControllerB in StartOfRound.Instance.allPlayerScripts)
            //{
            //    string username = playerControllerB.playerUsername;

            //    //comms.PlayerChannels.


            //    IDissonancePlayer player = playerControllerB.voicePlayerState.Tracker;
            //}

            //VoicePlayerState voicePlayerState = comms.FindPlayer(comms.LocalPlayerName);

            try
            {
                RefreshPlayerVoicePlaybackObjects();
            } catch (Exception e)
            {
                
            }

            // From Dissonance.Audio.Playback SamplePlaybackComponent
            // DebugSettings.Instance.EnablePlaybackDiagnostics = true;
            // DebugSettings.Instance.RecordFinalAudio = true;



                subscribePlayerVoice();

        }

        public void RefreshPlayerVoicePlaybackObjects()
        {
            PlayerVoiceIngameSettings[] playerVoiceIngameSettings = FindObjectsOfType<PlayerVoiceIngameSettings>();

            foreach(PlayerVoiceIngameSettings playerVoiceIngameSetting in playerVoiceIngameSettings)
            {
                if(ParanoiaModVoiceGrabber.Instances.ContainsKey(playerVoiceIngameSetting._playerState.Name))
                {
                    continue;
                }

                Plugin.Instance.Logger.LogInfo(" - - - - Grabbing " + playerVoiceIngameSetting._playerState.Name + " - - - - ");
                ParanoiaModVoiceGrabber paranoiaModVoiceGrabber = new ParanoiaModVoiceGrabber(playerVoiceIngameSetting);
                playerVoiceIngameSetting._playerState.OnStartedSpeaking += paranoiaModVoiceGrabber.OnStartedSpeaking;
                playerVoiceIngameSetting._playerState.OnStoppedSpeaking += paranoiaModVoiceGrabber.OnStoppedSpeaking;
            }

        }

        private void subscribePlayerVoice()
        {

            if (playerVoiceIngameSettings == null)
            {
                try
                {
                    playerVoiceIngameSettings = FindObjectOfType<PlayerVoiceIngameSettings>();
                    if (playerVoiceIngameSettings == null)
                    {
                        return;
                    }

                    playerVoiceIngameSettings._dissonanceComms.SubscribeToRecordedAudio(new ParanoiaModMicrophoneSubscriber());
                    Plugin.Instance.Logger.LogInfo(" - - - - Got PlayerVoiceIngameSettings - - - - ");
                }
                catch (Exception e)
                {

                }
            }
        }

        public void StartACoroutine(IEnumerator function)
        {
            StartCoroutine(function);
        }
    }

    internal class ParanoiaModMicrophoneSubscriber : MonoBehaviour, IMicrophoneSubscriber
    {
        ParanoiaModAudioBuffer audioBuffer;
        public ParanoiaModMicrophoneSubscriber()
        {
            audioBuffer = new ParanoiaModAudioBuffer("me");
            Plugin.Instance.Logger.LogInfo(" - - - - Microphone Subscriber - - - - ");

        }

        public void ReceiveMicrophoneData(ArraySegment<float> buffer, WaveFormat format)
        {
            float[] data = buffer.Array;
            audioBuffer.Capture(data);
            audioBuffer.sampleRate = format.SampleRate;
        }

        public void Reset()
        {
            Plugin.Instance.Logger.LogInfo(" - - - - Microphone Subscriber Reset - - - - ");
            audioBuffer.SaveToWav();
        }
    }

    internal class ParanoiaModVoiceGrabber
    {
        public static Dictionary<string, ParanoiaModVoiceGrabber> Instances = new Dictionary<string, ParanoiaModVoiceGrabber>();
        private PlayerVoiceIngameSettings playerVoiceIngameSettings;
        private bool isSpeaking = false;
        ParanoiaModAudioBuffer audioBuffer;
        SamplePlaybackComponent playback;

        public ParanoiaModVoiceGrabber(PlayerVoiceIngameSettings playerVoiceIngameSettings)
        {
            Plugin.Instance.Logger.LogInfo(" - - - - VoiceGrabbing "+ playerVoiceIngameSettings._playerState.Name + "- - - - ");

            Instances.Add(playerVoiceIngameSettings._playerState.Name, this);
            this.playerVoiceIngameSettings = playerVoiceIngameSettings;
            audioBuffer = new ParanoiaModAudioBuffer(playerVoiceIngameSettings._playerState.Name);
        }

        public void OnStartedSpeaking(VoicePlayerState voicePlayerState)
        {

            if(!isSpeaking)
            {
                Plugin.Instance.Logger.LogInfo(" - - - - StartedSpeaking " + playerVoiceIngameSettings._playerState.Name + "- - - - ");

                isSpeaking = true;
                ParanoiaModPersistent.Instance.StartACoroutine(TryGrabbing());
            }

        }

        public void OnStoppedSpeaking(VoicePlayerState voicePlayerState)
        {
            if(isSpeaking)
            {
                Plugin.Instance.Logger.LogInfo(" - - - - StartedSpeaking " + playerVoiceIngameSettings._playerState.Name + "- - - - ");

                isSpeaking = false;
            }
        }

        private IEnumerator TryGrabbing()
        {
            while(isSpeaking)
            {
                if(playback == null)
                {
                    playback = playerVoiceIngameSettings.GetComponent<SamplePlaybackComponent>();
                }
                SpeechSession session = (SpeechSession) playback.Session;


                audioBuffer.sampleRate = session.OutputWaveFormat.SampleRate;

                float[] data = new float[session.BufferCount];
                ArraySegment<float> dataSeg = new ArraySegment<float>(data, 0, session.BufferCount);
                session.Read(dataSeg);
                audioBuffer.Capture(dataSeg.Array);

                yield return new WaitForSeconds((float)session.BufferCount/ (float)session.OutputWaveFormat.SampleRate);
            }
            audioBuffer.SaveToWav();
        }
    }


    internal class ParanoiaModAudioBuffer
    {
        private int fileCount = 0;
        private string audioFolder;
        private List<float[]> bufferList = new List<float[]>();
        private float silenceTime = 0;
        private string player;
        public int sampleRate;

        public ParanoiaModAudioBuffer(string player)
        {
            this.player = player;
            Plugin.Instance.Logger.LogInfo(" - - - - Init Buffer (" + player + ") - - - - ");

            audioFolder = Path.Combine(Application.dataPath, "..", "ParanoiaModSamples");

            if (!Directory.Exists(audioFolder))
            {
                Directory.CreateDirectory(audioFolder);
            }

            audioFolder = audioFolder + "/" + player;

            if (!Directory.Exists(audioFolder))
            {
                Directory.CreateDirectory(audioFolder);
            }

        }

        public void SaveToWav()
        {
            if (bufferList.Count == 0) return;

            int bufferSize = 0;
            foreach (float[] buffer in bufferList)
            {
                bufferSize += buffer.Length;
            }

            fileCount++;
            AudioClip audioClip = AudioClip.Create("a", bufferSize, 1, sampleRate, false);

            int offset = 0;
            foreach (float[] buffer in bufferList)
            {
                audioClip.SetData(buffer, offset);
                offset += buffer.Length;
            }
            SavWav.Save(audioFolder + "/Sample_" + fileCount + ".wav", audioClip);
            Plugin.Instance.Logger.LogInfo(" - - - - Save (" + player + ") - - - - ");

            bufferList.Clear();
        }

        public void Capture(float[] data)
        {
            bool silence = true;

            if (data.Length > 0)
            {
                silence = data.Min() > -0.25f && data.Max() < 0.25f;
            }

            if (silenceTime + 1f < Time.time)
            {
                SaveToWav();

                if (silence)
                {
                    return;
                }
            }

            if (data.Length > 0)
            {
                float[] clonedData = (float[])data.Clone();
                bufferList.Add(clonedData);
                Plugin.Instance.Logger.LogInfo(" - - - - Buffer " + bufferList.Count + " (" + player + ") - - - - ");
            }

            if (!silence)
            {
                silenceTime = Time.time;
            }
        }


    }






    //  derived from Gregorio Zanon's script
    //  http://forum.unity3d.com/threads/119295-Writing-AudioListener.GetOutputData-to-wav-problem?p=806734&viewfull=1#post806734
    public static class SavWav
    {

        const int HEADER_SIZE = 44;

        public static bool Save(string filename, AudioClip clip)
        {
            if (!filename.ToLower().EndsWith(".wav"))
            {
                filename += ".wav";
            }

            var filepath = Path.Combine(Application.persistentDataPath, filename);

            Debug.Log(filepath);

            // Make sure directory exists if user is saving to sub dir.
            Directory.CreateDirectory(Path.GetDirectoryName(filepath));

            using (var fileStream = CreateEmpty(filepath))
            {

                ConvertAndWrite(fileStream, clip);

                WriteHeader(fileStream, clip);
            }

            return true; // TODO: return false if there's a failure saving the file
        }

        static FileStream CreateEmpty(string filepath)
        {
            var fileStream = new FileStream(filepath, FileMode.Create);
            byte emptyByte = new byte();

            for (int i = 0; i < HEADER_SIZE; i++) //preparing the header
            {
                fileStream.WriteByte(emptyByte);
            }

            return fileStream;
        }

        static void ConvertAndWrite(FileStream fileStream, AudioClip clip)
        {

            var samples = new float[clip.samples];

            clip.GetData(samples, 0);

            Int16[] intData = new Int16[samples.Length];
            //converting in 2 float[] steps to Int16[], //then Int16[] to Byte[]

            Byte[] bytesData = new Byte[samples.Length * 2];
            //bytesData array is twice the size of
            //dataSource array because a float converted in Int16 is 2 bytes.

            int rescaleFactor = 32767; //to convert float to Int16

            for (int i = 0; i < samples.Length; i++)
            {
                intData[i] = (short)(samples[i] * rescaleFactor);
                Byte[] byteArr = new Byte[2];
                byteArr = BitConverter.GetBytes(intData[i]);
                byteArr.CopyTo(bytesData, i * 2);
            }

            fileStream.Write(bytesData, 0, bytesData.Length);
        }

        static void WriteHeader(FileStream fileStream, AudioClip clip)
        {

            var hz = clip.frequency;
            var channels = clip.channels;
            var samples = clip.samples;

            fileStream.Seek(0, SeekOrigin.Begin);

            Byte[] riff = System.Text.Encoding.UTF8.GetBytes("RIFF");
            fileStream.Write(riff, 0, 4);

            Byte[] chunkSize = BitConverter.GetBytes(fileStream.Length - 8);
            fileStream.Write(chunkSize, 0, 4);

            Byte[] wave = System.Text.Encoding.UTF8.GetBytes("WAVE");
            fileStream.Write(wave, 0, 4);

            Byte[] fmt = System.Text.Encoding.UTF8.GetBytes("fmt ");
            fileStream.Write(fmt, 0, 4);

            Byte[] subChunk1 = BitConverter.GetBytes(16);
            fileStream.Write(subChunk1, 0, 4);

            UInt16 two = 2;
            UInt16 one = 1;

            Byte[] audioFormat = BitConverter.GetBytes(one);
            fileStream.Write(audioFormat, 0, 2);

            Byte[] numChannels = BitConverter.GetBytes(channels);
            fileStream.Write(numChannels, 0, 2);

            Byte[] sampleRate = BitConverter.GetBytes(hz);
            fileStream.Write(sampleRate, 0, 4);

            Byte[] byteRate = BitConverter.GetBytes(hz * channels * 2); // sampleRate * bytesPerSample*number of channels, here 44100*2*2
            fileStream.Write(byteRate, 0, 4);

            UInt16 blockAlign = (ushort)(channels * 2);
            fileStream.Write(BitConverter.GetBytes(blockAlign), 0, 2);

            UInt16 bps = 16;
            Byte[] bitsPerSample = BitConverter.GetBytes(bps);
            fileStream.Write(bitsPerSample, 0, 2);

            Byte[] datastring = System.Text.Encoding.UTF8.GetBytes("data");
            fileStream.Write(datastring, 0, 4);

            Byte[] subChunk2 = BitConverter.GetBytes(samples * channels * 2);
            fileStream.Write(subChunk2, 0, 4);

            //		fileStream.Close();
        }
    }
}

