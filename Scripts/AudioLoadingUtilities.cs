﻿using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace JimmysUnityUtilities
{
    /// <summary>
    /// For loading audio from files on disk.
    /// </summary>
    public static class AudioLoadingUtilities
    {
        /// <summary>
        /// Take an audio file on disk (supported formats are .ogg, .wav, and .mp3) and load it into a Unity-playable <see cref="AudioClip"/>.
        /// The returned clip will be compressed in memory.
        /// </summary>
        /// <param name="filePath">The full, rooted path of the file.</param>
        /// <param name="onFinishedLoadingCallback">It will take a bit to download and process the file; use this callback to execute code on the clip after that process is finished.</param>
        public static void LoadAudioFromDisk(string filePath, Action<AudioClip> onFinishedLoadingCallback)
        {
            EnsureFileCanBeLoaded(filePath, out var audioType);

            CoroutineUtility.Run(LoadFileRoutine());


            IEnumerator LoadFileRoutine()
            {
                string uri = FileUtilities.FilePathToURI(filePath);
                using (var webRequest = UnityWebRequestMultimedia.GetAudioClip(uri, audioType))
                {
                    webRequest.SendWebRequest();

                    while (!webRequest.isNetworkError && !webRequest.isDone)
                        yield return null;

                    if (webRequest.isNetworkError)
                    {
                        Debug.LogError($"Error loading audio at {filePath}!{Environment.NewLine}{webRequest.error}");
                        yield break;
                    }

                    var clipDownloader = ((DownloadHandlerAudioClip)webRequest.downloadHandler);
                    var clip = clipDownloader.audioClip;

                    try
                    {
                        onFinishedLoadingCallback(clip);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        Debug.LogError("There was an exception running the callback. AudioClip will be destroyed to prevent memory leaks.");
                        UnityEngine.Object.Destroy(clip);
                    }
                }
            }
        }

        /// <summary>
        /// Take an audio file on disk (supported formats are .ogg, .wav, and .mp3) and load it into a Unity-playable <see cref="AudioClip"/>.
        /// The returned clip will be streamed from disk, and should be destroyed (see <see cref="UnityEngine.Object.Destroy(UnityEngine.Object)"/>) after it is finished playing.
        /// </summary>
        /// <param name="filePath">The full, rooted path of the file.</param>
        /// <param name="onReadyToPlayCallback">The file will take a bit to download and convert; use this to execute code on it after that process is finished.</param>
        public static void StreamAudioFromDisk(string filePath, Action<AudioClip> onReadyToPlayCallback)
        {
            EnsureFileCanBeLoaded(filePath, out var audioType);

            CoroutineUtility.Run(LoadFileRoutine());

            IEnumerator LoadFileRoutine()
            {
                const ulong minDownloadedBytesWhenStreamingCanBegin = 1024;

                string uri = FileUtilities.FilePathToURI(filePath);
                using (var webRequest = UnityWebRequestMultimedia.GetAudioClip(uri, audioType))
                {
                    var clipDownloader = (DownloadHandlerAudioClip)webRequest.downloadHandler;
                    clipDownloader.streamAudio = true; // Due to a Unity bug this is actually totally non-functional... https://forum.unity.com/threads/downloadhandleraudioclip-streamaudio-is-ignored.699908/

                    webRequest.SendWebRequest();
                    while (!webRequest.isNetworkError && webRequest.downloadedBytes < minDownloadedBytesWhenStreamingCanBegin)
                        yield return null;

                    if (webRequest.isNetworkError)
                    {
                        Debug.LogError($"Error streaming music at {filePath}!{Environment.NewLine}{webRequest.error}");
                        yield break;
                    }

                    var clip = clipDownloader.audioClip;

                    try
                    {
                        onReadyToPlayCallback(clip);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        Debug.LogError("There was an exception running the callback. AudioClip will be destroyed to prevent memory leaks.");
                        webRequest.Abort();
                        UnityEngine.Object.Destroy(clip);
                    }
                }
            }
        }


        private static void EnsureFileCanBeLoaded(string filePath, out AudioType audioType)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Cannot load audio at '{filePath}' because the file does not exist");

            string extension = Path.GetExtension(filePath);
            if (!CanLoadFileType(extension, out audioType))
                throw new Exception($"Cannot load audio file at {filePath}; wrong type. Supported types are .ogg, .wav and .mp3.");


            bool CanLoadFileType(string _extension, out AudioType _audioType)
            {
                switch (_extension.ToLower())
                {
                    case ".mp3":
                        _audioType = AudioType.MPEG;
                        return true;
                    case ".ogg":
                        _audioType = AudioType.OGGVORBIS;
                        return true;
                    case ".wav":
                        _audioType = AudioType.WAV;
                        return true;

                    default:
                        _audioType = AudioType.UNKNOWN;
                        return false;
                }
            }
        }
    }
}