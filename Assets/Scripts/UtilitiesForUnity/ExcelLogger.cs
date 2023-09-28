using UnityEngine;
using System.IO;

/// <summary>
/// Implements the recording of formatted data in a csv file
/// </summary>
public class ExcelLogger : MonoBehaviour
{
    [SerializeField] private string filename = "";
    [SerializeField] private string path;

    void Start()
    {
        path = Application.dataPath + "/" + filename + ".csv";
    }
    /// <summary>
    /// Sets name of csv file
    /// </summary>
    /// <param name="filename">File name</param>
    public void SetFilename(string filename)
    {
        this.filename = filename;
    }
    /// <summary>
    /// Sets path of csv file
    /// </summary>
    /// <param name="path">File path</param>
    public void SetPath(string path)
    {
        this.path = path;
    }
    /// <summary>
    /// Writes headers in csv file
    /// </summary>
    /// <param name="head">Headers separated by ; or , or tab or space</param>
    public void WriteHead(string head)
    {
        TextWriter textWriter = new StreamWriter(path, true);
        textWriter.WriteLine(head);
        textWriter.Close();
    }
    /// <summary>
    /// Writes line in csv file
    /// </summary>
    /// <param name="line">Table row separated by ; or , or tab or space</param>
    public void WriteLine(string line)
    {
        TextWriter textWriter = new StreamWriter(path, true);
        textWriter.WriteLine(line);
        textWriter.Close();
    }
}
