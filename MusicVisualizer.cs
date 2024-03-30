using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class MusicVisualizer : MonoBehaviour
{
    [ContextMenu("Reset Values to Default")]
    void DoSomething()
    {
        OutputType = OutputVoltageOrDecibel.Decibels;
        VoltageMultiplier = 1f;
        DecibelsMultiplier = 1f;
        ReferenceValue = .0005f;
        Smoothing = .18f;
        Threshold = 0f;
    }

    //ENUMS
    public enum OutputVoltageOrDecibel { Voltage, Decibels }
    public enum SampleCountOptions { _8192, _4096, _2048, _1024, _512, _256, _128, _64 }

    //Public Variables
    public AudioSource audioSource;
    [Space(2, order = 0)]
    [Header("OUTPUT", order = 1)]
    public SampleCountOptions SampleCount = SampleCountOptions._2048;
    public OutputVoltageOrDecibel OutputType = OutputVoltageOrDecibel.Decibels;
    [Header("Voltage Output")]
    public float VoltageMultiplier = 1f;
    [Header("Decibel Output")]
    public float DecibelsMultiplier = 1f;
    [Range(.0000001f, .01f)]
    public float ReferenceValue = .0005f;
    [Header("Smoothing and Clamping")]
    public float Smoothing = .18f;
    [Header("dB Threshold")]
    public float Threshold = 0f;
    [Space(20, order = 2)]
    public float[] MusicalDecibels { get { return smoothedMusicalDecibels; } }
    public float OverallDecibelLevel { get { return smoothedOverallDecibelLevel; } }

    // Private Variables
    private float[] smoothedMusicalDecibels = new float[128];
    private float smoothedOverallDecibelLevel;
    private float overallDecibelLevel;
    private float previousOverallDecibels;
    private float overallDecibleLevelSmoothingVelocity;
    private float[] spectrum;
    private float[] musicalDecibels = new float[128];
    private float[] musicalFrequencies = new float[128];
    private float[] previousDecibelReadings = new float[128];
    private float[] dampVelocities = new float[128];
    //private float equalLoudnessContourMultiplier;
    private float frequencyIncrement;
    private int sampleCount;
    private int spectrumIndex;
    private float rms;
    private float sampleSum;
    private float maxDB;
    private float mainFreq;
    private float lastMainFreq = 0f;

    void Start()
    {
        DetermineSampleCount();
        ResizeSpectrumArray();
        DefineMusicalFrequencies();
    }

    void Update()
    {
        CallGetSpectrumData();
        ProcessSpectrumData();

        DetermineSampleCount();
        if (spectrum.Length != sampleCount)
        {
            ResizeSpectrumArray();
            DefineMusicalFrequencies();
        }
    }

    void DetermineSampleCount()
    {
        switch (SampleCount)
        {
            case SampleCountOptions._8192:
                sampleCount = 8192;
                break;
            case SampleCountOptions._4096:
                sampleCount = 4096;
                break;
            case SampleCountOptions._2048:
                sampleCount = 2048;
                break;
            case SampleCountOptions._1024:
                sampleCount = 1024;
                break;
            case SampleCountOptions._512:
                sampleCount = 512;
                break;
            case SampleCountOptions._256:
                sampleCount = 256;
                break;
            case SampleCountOptions._128:
                sampleCount = 128;
                break;
            case SampleCountOptions._64:
                sampleCount = 64;
                break;
        }
    }

    void ResizeSpectrumArray()
    {
        //resize "spectrum" array to match sampleCount, this array will store the raw data we get from getspectrumdata
        spectrum = new float[sampleCount];
    }

    void DefineMusicalFrequencies()
    {
        //function to populate the musicalFrequencies array with all the frequencies represented by an equal temperament tuned piano, plus one octave lower and two octaves higher. 
        musicalFrequencies[12] = 27.5f; // A0 - this is the lowest note on a piano and is 27.5hz
        musicalFrequencies[13] = 29.1352f; // B0 Flat
        musicalFrequencies[14] = 30.8677f; // B0
        musicalFrequencies[15] = 32.7032f; // C1
        musicalFrequencies[16] = 34.6478f; // C1 Sharp
        musicalFrequencies[17] = 36.7081f; // D1
        musicalFrequencies[18] = 38.8909f; // E1 Flat
        musicalFrequencies[19] = 41.2034f; // E1
        musicalFrequencies[20] = 43.6535f; // F
        musicalFrequencies[21] = 46.2493f; // F1 Sharp
        musicalFrequencies[22] = 48.9994f; // G
        musicalFrequencies[23] = 51.9131f; // A1 Flat
        //loop to store  values representing an octave below the  piano. 
        for (int i = 0; i < 12; i++)
        {
            musicalFrequencies[i] = musicalFrequencies[i + 12] / 2f;
        }
        //loop to gather all the  remaining frequencies up from our lowest  piano octave above. 
        for (int i = 24; i < 128; i++)
        {
            musicalFrequencies[i] = musicalFrequencies[i - 12] * 2f;
        }
        //frequency band increments based on input audio sampling rate 
        frequencyIncrement = (AudioSettings.outputSampleRate / 2f) / sampleCount;
    }

    void CallGetSpectrumData()
    {
        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
    }

    void ProcessSpectrumData()
    {
        sampleSum = 0f; //resetting sample sum for the overall decibel calculation further below
        maxDB = 0f;
        //converting the voltages from spectrum array into decibles
        for (int i = 0; i < musicalFrequencies.Length; i++)
        {
            spectrumIndex = (int)Mathf.Round(musicalFrequencies[i] / frequencyIncrement); 
            //index of the "spectrum" array should we sample from to get this particular frequency

            
            if (OutputType == OutputVoltageOrDecibel.Decibels)
            {
                //store selected sample and convert it from voltage/signal value to a decibel value
                //and we also multiply by a multiplier the user can adjust
                musicalDecibels[i] = 20f * Mathf.Log10(spectrum[spectrumIndex] / ReferenceValue) * DecibelsMultiplier;
            }
            else
            {
                musicalDecibels[i] = spectrum[spectrumIndex] * VoltageMultiplier;
            }
            //Smooth samples, setting it up such that decreasing values have an interpolation speed, but increasing values go straight to the current new value.
            if (musicalDecibels[i] < previousDecibelReadings[i])
            {   //smoothing if values are decreasing
                smoothedMusicalDecibels[i] = Mathf.SmoothDamp(previousDecibelReadings[i], 0f, ref dampVelocities[i], Smoothing);
                smoothedMusicalDecibels[i] = Mathf.Max(smoothedMusicalDecibels[i], musicalDecibels[i]);
                //interpolating negative values to 0
            }
            else
            {   //no smoothing if values are increasing
                smoothedMusicalDecibels[i] = musicalDecibels[i];
            }
            previousDecibelReadings[i] = smoothedMusicalDecibels[i];
            //adding to samplesum to have the sum of our frequency amplitudes by the end of the for loop
            sampleSum += spectrum[spectrumIndex] * spectrum[spectrumIndex];
            if (musicalDecibels[i] > maxDB)
            {
                maxDB = musicalDecibels[i];
                mainFreq = musicalFrequencies[i];
            }
        }
        if(maxDB > Threshold && lastMainFreq!=mainFreq)
        {
            Debug.Log(mainFreq);
            lastMainFreq = mainFreq;
        }


        // OVERALL VOLUME/DECIBEL OUTPUT

        //root mean square (rms) to calculate the overall volume and decible level
        rms = Mathf.Sqrt(sampleSum / musicalDecibels.Length); // rms(root mean square) = square root of average
        overallDecibelLevel = 20 * Mathf.Log10(rms / ReferenceValue) * DecibelsMultiplier; 
        //convert to decibels from voltage based rms value

        //smoothing the results
        if (overallDecibelLevel < previousOverallDecibels)
        {
            smoothedOverallDecibelLevel = Mathf.SmoothDamp(previousOverallDecibels, 0f, ref overallDecibleLevelSmoothingVelocity, Smoothing);
            smoothedOverallDecibelLevel = Mathf.Max(smoothedOverallDecibelLevel, overallDecibelLevel);
        }
        else
        {
            smoothedOverallDecibelLevel = overallDecibelLevel;
        }
        previousOverallDecibels = smoothedOverallDecibelLevel;
    }
}
