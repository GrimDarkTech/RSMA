using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SoundPitchDataTransfer : RSMADataTransferSlave
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
