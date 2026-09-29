using UnityEditor;
using UnityEngine;

public class GoofySoundtrackImporter : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (assetPath != "Assets/Resources/Audio/GoofyShuffle.wav") return;
        var importer = (AudioImporter)assetImporter;
        importer.forceToMono = false;
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        importer.defaultSampleSettings = settings;
    }
}
