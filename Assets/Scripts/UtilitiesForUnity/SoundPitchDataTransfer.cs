using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SoundPitchDataTransfer : DataTransgerSlaveScript
{
    public AudioSource audioSource;
    private void Start()
    {
        audioSource = gameObject.GetComponent<AudioSource>();
    }
    public override void ReciveData(string recivedData)
    {
        audioSource.pitch = 1.3f + Mathf.Abs(float.Parse(recivedData)/100);
    }
}
