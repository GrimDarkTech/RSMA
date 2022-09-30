using UnityEngine;
using System.IO;

public class ExeclLoggerDataTransfer : DataTransgerSlaveScript
{
    [SerializeField] private string filename = "";
    string path;

    public void SetFilename(string newFilename)
    {
        filename = newFilename;
    }
    public override void ReciveData(string recivedData)
    {
        TextWriter textWriter = new StreamWriter(path, true);
        textWriter.WriteLine(recivedData);
        textWriter.Close();

        textWriter = new StreamWriter(path, true);
        textWriter.WriteLine(recivedData);
        textWriter.Close();
    }
    void Start()
    {
        path = Application.dataPath + "/" + filename + ".csv";
    }
}
