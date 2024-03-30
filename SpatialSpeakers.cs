using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpatialSpeakers : MonoBehaviour
{
    public MusicVisualizer musicVisualizer;
    public AudioSource a1, a2, a3, a4, a5, a6, a7, a8, a9, a10;
    public float[] outputVolume = new float[10];
    private float voltSum = 0f;
    int k = 0;
    void Start()
    {
        
    }

    void Update()
    {
        for(int i = 0; i<10; i++)
        {
            voltSum = 0f;
            k = i * 12;
            for(int j=k; j<(k+12); j++)
            {
                voltSum += musicVisualizer.MusicalDecibels[j] * musicVisualizer.MusicalDecibels[j];
            }
            outputVolume[i] = Mathf.Sqrt(voltSum/12);
        }
        a1.volume = outputVolume[0];
        a2.volume = outputVolume[1];
        a3.volume = outputVolume[2];
        a4.volume = outputVolume[3];
        a5.volume = outputVolume[4];
        a6.volume = outputVolume[5];
        a7.volume = outputVolume[6];
        a8.volume = outputVolume[7];
    }
}
