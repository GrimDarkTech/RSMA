using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Generates docfx-like documentation file via markdown format
/// </summary>
public class AutoDocsMD : MonoBehaviour
{
    /// <summary>
    /// Absolute file directory path
    /// </summary>
    public string filepath;

    /// <summary>
    /// .cs file name
    /// </summary>
    public string filename;

    /// <summary>
    /// Absolute path to doc folder
    /// </summary>
    public string docsFolderPath;

    public List<string> lines;

    public List<string> docStrings;

    public List<SerializedClass> classes;

    [ContextMenu("Generate MD doc")]
    public void GenerateMD()
    {
        lines = new List<string>();
        lines.Clear();

        classes.Clear();

        StreamReader streamReader = new StreamReader(filepath + "/" + filename);

        while (true)
        {
            string line = streamReader.ReadLine();

            if(line is null)
            {
                break;
            }

            if(line != "")
            {
                lines.Add(line);
            }
            
        }

        streamReader.Close();

        foreach (string line in lines)
        {
            if(line.IndexOf("class") > -1)
            {
                var words = line.Split(" ");
                
                for(int i = 0; i < words.Length; i++)
                {
                    if(words[i] == "class")
                    {
                        Type classType = Type.GetType(words[i + 1]);

                        if(classType != null)
                        {
                            SerializedClass serClass = new SerializedClass();

                            serClass.name = words[i + 1];

                            serClass.fields = new List<SerializedField>();

                            var fields = classType.GetFields();
                            
                            foreach (var field in fields)
                            {
                                SerializedField serField = new SerializedField();
                                serField.name = field.Name;
                                serField.type = field.FieldType.ToString();

                                serClass.fields.Add(serField);
                            }

                            serClass.properties = new List<SerializedProperty>();

                            var properties = classType.GetProperties();

                            foreach(var propertie in properties)
                            {

                                SerializedProperty serPropertie = new SerializedProperty();
                                serPropertie.name = propertie.Name;
                                serPropertie.type = propertie.PropertyType.ToString();

                                serClass.properties.Add(serPropertie);
                            }

                            serClass.methods = new List<SerializedMethod>();

                            var methods = classType.GetMethods();

                            foreach (var method in methods)
                            {

                                SerializedMethod serMethod = new SerializedMethod();
                                serMethod.name = method.Name;

                                serMethod.parameters = new List<SerializedMethodParam>();

                                serClass.methods.Add(serMethod);
                            }

                            classes.Add(serClass);

                            break;
                        }
                    }
                }
            }
        }

        docStrings = new List<string>();
        docStrings.Clear();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.IndexOf("<summary>") > -1)
            {
                docStrings.Add("<summary>");
            }
            else if (line.IndexOf("</summary>") > -1)
            {
                docStrings.Add("</summary>");

                if(lines[i + 1].IndexOf("<param name") == -1 && lines[i + 1].IndexOf("<returns>") == -1)
                {
                    docStrings.Add(lines[i + 1]);
                }
            }
            else if (line.IndexOf("<param name") > -1)
            {
                var words = line.Split("\">");
                words[0] = words[0].Replace("<param name=\"", "");
                words[1] = words[1].Replace("</param>", "");
                docStrings.Add("<p>" + words[0].Replace("///", "") + "=" + words[1]);

                if (lines[i + 1].IndexOf("<param name") == -1 && lines[i + 1].IndexOf("<returns>") == -1)
                {
                    docStrings.Add(lines[i + 1]);
                }
            }
            else if (line.IndexOf("<returns>") > -1)
            {
                string ret = line.Replace("///", "");
                ret = ret.Replace("<returns>", "");
                ret = ret.Replace("</returns>", "");

                docStrings.Add("<r>" + ret);

                docStrings.Add(lines[i + 1]);
            }
            else if (line.IndexOf("///") > -1)
            {
                docStrings.Add(line.Replace("///", ""));
            }
        }

        for (int cn = 0; cn < classes.Count; cn++)
        {
            var serClass = classes[cn];

            for (int i = 0; i < docStrings.Count; i++)
            {
                var docLine = docStrings[i];

                if (docLine.IndexOf("class " + serClass.name) > -1)
                {
                    var description = "";

                    if (docStrings[i - 1].IndexOf("</summary>") > -1)
                    {
                        int j = 2;
                        while(docStrings[i - j].IndexOf("<summary>") == -1)
                        {
                            description += docStrings[i - j].Replace("///", "");
                            j++;
                        }
                    }

                    serClass.description = description;
                }
                else
                {
                    for (int fn = 0; fn < serClass.fields.Count; fn++)
                    {
                        var field = serClass.fields[fn];

                        if (docLine.IndexOf(field.name) > -1)
                        {
                            var description = "";

                            if (docStrings[i - 1].IndexOf("</summary>") > -1)
                            {
                                int j = 2;
                                while (docStrings[i - j].IndexOf("<summary>") == -1)
                                {
                                    description += docStrings[i - j].Replace("///", "");
                                    j++;
                                }
                            }

                            field.description = description;
                        }

                        serClass.fields[fn] = field;
                    }

                    for (int pn = 0; pn < serClass.properties.Count; pn++)
                    {
                        var propertie = serClass.properties[pn];

                        if (docLine.IndexOf(propertie.name) > -1)
                        {
                            var description = "";

                            if (docStrings[i - 1].IndexOf("</summary>") > -1)
                            {
                                int j = 2;
                                while (docStrings[i - j].IndexOf("<summary>") == -1)
                                {
                                    description += docStrings[i - j].Replace("///", "");
                                    j++;
                                }
                            }

                            propertie.description = description;
                        }

                        serClass.properties[pn] = propertie;
                    }

                    for (int mn = 0; mn < serClass.properties.Count; mn++)
                    {
                        var method = serClass.methods[mn];

                        if (docLine.IndexOf(method.name) > -1)
                        {
                            var description = "";

                            if (docStrings[i - 1].IndexOf("</summary>") > -1)
                            {
                                int j = 2;
                                while (docStrings[i - j].IndexOf("<summary>") == -1)
                                {
                                    description += docStrings[i - j].Replace("///", "");
                                    j++;
                                }
                            }

                            if (docStrings[i - 1].IndexOf("<r>") > -1)
                            {
                                method.retunrs = docStrings[i - 1].Replace("<r>", "");
                            }

                            if(docStrings[i - 1].IndexOf("<p>") > -1)
                            {
                                int j = 1;

                                SerializedMethodParam param = new SerializedMethodParam();

                                while (docStrings[i - j].IndexOf("<p>") > -1)
                                {
                                    var str = "";
                                    str = docStrings[i - j].Replace("<p>", "");

                                    param.name = str.Split("=")[0];
                                    param.description = str.Split("=")[1];

                                    method.parameters.Add(param);

                                    j++;
                                }
                            }


                            method.description = description;
                        }

                        serClass.methods[mn] = method;
                    }
                }
            }

            classes[cn] = serClass;
        }
    }

    [Serializable]
    public struct SerializedClass
    {
        public string name;
        public string description;
        public List<SerializedField> fields;
        public List<SerializedProperty> properties;
        public List<SerializedMethod> methods;
    }
    [Serializable]
    public struct SerializedField
    {
        public string name;
        public string description;
        public string type;
    }
    [Serializable]
    public struct SerializedProperty
    {
        public string name;
        public string description;
        public string type;
    }
    [Serializable]
    public struct SerializedMethod
    {
        public string name;
        public string declaration;
        public string description;
        public string retunrs;
        public List<SerializedMethodParam> parameters;
    }
    [Serializable]
    public struct SerializedMethodParam
    {
        public string name;
        public string description;
    }
}
