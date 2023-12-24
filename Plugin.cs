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

            ParanoiaModMicrophoneSubscriber paranoiaModMicrophoneSubscriber = new ParanoiaModMicrophoneSubscriber();
            paranoiaModMicrophoneSubscriber.Awake();
        }

        private void Update()
        {
            if (playerVoiceIngameSettings == null) { 
                try
                {
                    playerVoiceIngameSettings = FindObjectOfType<PlayerVoiceIngameSettings>();
                    if(playerVoiceIngameSettings == null )
                    {
                        return;
                    }

                    playerVoiceIngameSettings._dissonanceComms.SubscribeToRecordedAudio(ParanoiaModMicrophoneSubscriber.Instance);
                    Plugin.Instance.Logger.LogInfo(" - - - - Got PlayerVoiceIngameSettings - - - - ");
                }
                catch (Exception e)
                {

                }
            }

        }

    }

    internal class ParanoiaModMicrophoneSubscriber : MonoBehaviour, IMicrophoneSubscriber
    {
        public static ParanoiaModMicrophoneSubscriber Instance { get; private set; }


        private int fileCount = 0;
        private string audioFolder;
        private List<float[]> bufferList = new List<float[]>();
        private WaveFormat lastWaveFormat;
        private float silenceTime = 0;

        public void Awake()
        {
            if (Instance == null)
            {
                Instance = this;

                Plugin.Instance.Logger.LogInfo(" - - - - Awake Microphone - - - - ");

                audioFolder = Path.Combine(Application.dataPath, "..", "ParanoiaModSamples");

                if (!Directory.Exists(audioFolder))
                {
                    Directory.CreateDirectory(audioFolder);
                }
                if (!Directory.Exists(audioFolder + "/OwnPlayerAudio"))
                {
                    Directory.CreateDirectory(audioFolder + "/OwnPlayerAudio");
                }
                if (!Directory.Exists(audioFolder + "/OtherPlayerAudio"))
                {
                    Directory.CreateDirectory(audioFolder + "/OtherPlayerAudio");
                }
            }
        }

        public void ReceiveMicrophoneData(ArraySegment<float> buffer, WaveFormat format)
        {
            float[] data = buffer.Array;
            Plugin.Instance.Logger.LogInfo(" - - - - Buffer "+bufferList.Count+" - - - - ");


            bool silence = data.Min() > -0.25f && data.Max() < 0.25f;

            if (silenceTime+1f < Time.time)
            {
                SaveToWav();

                if (silence)
                {
                    return;
                }
            } 

            lastWaveFormat = format;
            float[] clonedData = (float[]) data.Clone();
            bufferList.Add(clonedData);

            if (!silence)
            {
                silenceTime = Time.time;
            }
        }

        public void SaveToWav()
        {
            if (bufferList.Count == 0) return;

            int bufferSize = 0;
            foreach(float[] buffer in  bufferList)
            {
                bufferSize+= buffer.Length;
            }

            fileCount++;
            AudioClip audioClip = AudioClip.Create("a", bufferSize, lastWaveFormat.Channels, lastWaveFormat.SampleRate, false);

            int offset = 0;
            foreach (float[] buffer in bufferList)
            {
                audioClip.SetData(buffer, offset);
                offset+= buffer.Length;
            }
            SavWav.Save(audioFolder + "/OwnPlayerAudio/Custom" + fileCount + ".wav", audioClip);
            Plugin.Instance.Logger.LogInfo(" - - - - Save - - - - ");
            Plugin.Instance.Logger.LogInfo(audioFolder + "/OwnPlayerAudio/Custom" + fileCount + ".wav");

            bufferList.Clear();
        }

        public void Reset()
        {
            Plugin.Instance.Logger.LogInfo(" - - - - Reset - - - - ");
            SaveToWav();
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

