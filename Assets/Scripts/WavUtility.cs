using System;
using System.IO;
using UnityEngine;

namespace Seeman
{
    public static class WavUtility
    {
        public static byte[] FromAudioClip(AudioClip clip)
        {
            float[] data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            int sampleRate = clip.frequency;
            int channels = clip.channels;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                int byteCount = data.Length * 2;
                bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + byteCount);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1);
                bw.Write((short)channels);
                bw.Write(sampleRate);
                bw.Write(sampleRate * channels * 2);
                bw.Write((short)(channels * 2));
                bw.Write((short)16);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                bw.Write(byteCount);
                foreach (float f in data)
                {
                    short s = (short)(Mathf.Clamp(f, -1f, 1f) * short.MaxValue);
                    bw.Write(s);
                }
                return ms.ToArray();
            }
        }

        public static AudioClip ToAudioClip(byte[] wav, string name = "wav")
        {
            if (wav == null || wav.Length < 44) return null;
            int pos = 12;
            int channels = 1, sampleRate = 22050, bits = 16;
            int dataPos = -1, dataLen = 0;
            while (pos + 8 <= wav.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(wav, pos, 4);
                int size = BitConverter.ToInt32(wav, pos + 4);
                if (id == "fmt ")
                {
                    channels = BitConverter.ToInt16(wav, pos + 10);
                    sampleRate = BitConverter.ToInt32(wav, pos + 12);
                    bits = BitConverter.ToInt16(wav, pos + 22);
                }
                else if (id == "data") { dataPos = pos + 8; dataLen = size; break; }
                pos += 8 + size + (size & 1);
            }
            if (dataPos < 0) return null;
            if (dataLen > wav.Length - dataPos) dataLen = wav.Length - dataPos;

            float[] samples;
            if (bits == 16)
            {
                int count = dataLen / 2;
                samples = new float[count];
                for (int i = 0; i < count; i++)
                    samples[i] = BitConverter.ToInt16(wav, dataPos + i * 2) / 32768f;
            }
            else if (bits == 32)
            {
                int count = dataLen / 4;
                samples = new float[count];
                for (int i = 0; i < count; i++)
                    samples[i] = BitConverter.ToSingle(wav, dataPos + i * 4);
            }
            else return null;

            if (samples.Length == 0 || channels <= 0) return null;
            var clip = AudioClip.Create(name, samples.Length / channels, channels, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
