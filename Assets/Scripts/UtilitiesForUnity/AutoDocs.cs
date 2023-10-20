using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Generates docfx-like documentation file via markdown format
/// </summary>
public class AutoDocs : MonoBehaviour
{
    /// <summary>
    /// Absolute .cs file path
    /// </summary>
    public string filepath;

    /// <summary>
    /// .cs file name
    /// </summary>
    /// <remarks>
    /// file name without .cs
    /// </remarks>
    public string filename;

    /// <summary>
    /// Absolute path to doc folder
    /// </summary>
    public string docsFolderPath;

    public List<string> lines;
    public List<SerializedClass> classes;

    [ContextMenu("GenerateMDFile")]
    public void GenerateMDfromCS()
    {
        try
        {
            lines = FileToStr();
            classes = FindClasses(lines);

            lines.Clear();

            
            
            foreach(SerializedClass sClass in classes)
            {
                lines.Add($"# {sClass.name}");
                lines.Add($"[switch to API](../../../Documentation/ScriptingAPI/en/{filename}.md)");
                lines.Add("");
                lines.Add($"{sClass.description}");
                lines.Add("");
                if(sClass.consts.Count > 0)
                {
                    lines.Add("## Constants");
                    lines.Add("| Constant | Description | Type |");
                    lines.Add("|--|--|--|");
                    foreach (SerializedConst sConst in sClass.consts)
                    {
                        lines.Add($"|{sConst.name}|{sConst.description}|{sConst.type}|");
                    }
                }
                if(sClass.fields.Count > 0)
                {
                    lines.Add("## Fields");
                    lines.Add("| Field | Description | Type |");
                    lines.Add("|--|--|--|");
                    foreach (SerializedField field in sClass.fields)
                    {
                        lines.Add($"|{field.name}|{field.description}|{field.type}|");
                    }
                }
                if(sClass.properties.Count > 0)
                {
                    lines.Add("## Properties");
                    lines.Add("| Property | Description | Type |");
                    lines.Add("|--|--|--|");
                    foreach (SerializedProperty property in sClass.properties)
                    {
                        lines.Add($"|{property.name}|{property.description}|{property.type}|");
                    }
                }
                if(sClass.methods.Count > 0) 
                {
                    lines.Add("## Methods");
                    lines.Add("| Declaration | Description | Returns |");
                    lines.Add("|--|--|--|");
                    foreach (SerializedMethod method in sClass.methods)
                    {
                        lines.Add($"|{method.name}|{method.description}|{method.retunrs}|");
                        if(method.parameters.Count > 0)
                        {
                            lines.Add("### Parameters");
                            lines.Add("| Name | Description |");
                            lines.Add("|--|--|");
                            foreach (SerializedMethodParam param in method.parameters)
                            {
                                lines.Add($"|{param.name}|{param.description}|");
                            }
                        }

                    }
                }


            }

            StreamWriter sw = new StreamWriter($"{docsFolderPath}\\{filename}.md");
            
            foreach(string line in lines)
            {
                sw.WriteLine(line);
            }

            lines.Clear();

            sw.Close();
        }
        catch (Exception e)
        {
            Debug.LogError("Exception: " + e.Message);
        }

    }
    private List<string> FileToStr()
    {
        List<string> lines = new List<string>();
        try
        {
            StreamReader sr = new StreamReader($"{filepath}\\{filename}.cs");

            while (true)
            {
                string line = sr.ReadLine();
                if (line is null)
                {
                    break;
                }
                if (line != "" && (line.Contains("///") || line.Contains("public")))
                {
                    line = line.Replace("///", "");
                    line = line.Replace("public", "");
                    line = line.Replace("<summary>", "/s");
                    line = line.Replace("</summary>", "/e");
                    line = line.Replace("<remarks>", "/rm");
                    line = line.Replace("</remarks>", "");
                    line = line.Replace("<param name=\"", "/p");
                    line = line.Replace("\">", "/s");
                    line = line.Replace("</param>", "");
                    line = line.Replace("<returns>", "/rt");
                    line = line.Replace("</returns>", "");
                    line = line.Replace(";", "");
                    line = line.TrimStart();
                    line = line.TrimEnd();

                    lines.Add(line);
                }
            }
            sr.Close();
        }
        catch (Exception e)
        {
            Debug.LogError("Exception: " + e.Message);
        }
        return lines;
    }
    public List<SerializedClass> FindClasses(List<string> lines)
    {
        List<SerializedClass> serializedClasses = new List<SerializedClass>();

        for(int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains("class"))
            {
                SerializedClass serializedClass = new SerializedClass();
                serializedClass.consts = new List<SerializedConst>();
                serializedClass.fields = new List<SerializedField>();
                serializedClass.properties = new List<SerializedProperty>();
                serializedClass.methods = new List<SerializedMethod>();
                lines[i] = lines[i].Split(':')[0];
                lines[i] = lines[i].Replace("class", ""); ;
                lines[i] = lines[i].Trim();
                var linesSplited = lines[i].Split(' ');
                serializedClass.name = linesSplited[linesSplited.Count() - 1];
                serializedClass.description = FindDescription(lines, i);

                var fieldsNames = new List<string>();
                var propertiesNames = new List<string>();
                var methodsNames = new List<string>();

                Type type = Type.GetType(serializedClass.name);
                if (type != null)
                {
                    FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                    PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                    foreach(FieldInfo fieldInfo in fields)
                    {
                        fieldsNames.Add(fieldInfo.Name);
                    }
                    foreach(PropertyInfo propertyInfo in properties)
                    {
                        propertiesNames.Add(propertyInfo.Name);
                    }
                    foreach (MethodInfo methodInfo in methods)
                    {
                        methodsNames.Add(methodInfo.Name);
                    }
                }

                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (lines[j].Contains("const"))
                    {
                        SerializedConst serializedConst = new SerializedConst();
                        serializedConst.name = lines[j].Split(" ", StringSplitOptions.RemoveEmptyEntries)[2];
                        serializedConst.type = lines[j].Split(" ", StringSplitOptions.RemoveEmptyEntries)[1];
                        serializedConst.value = lines[j].Split("=", StringSplitOptions.RemoveEmptyEntries)[1];
                        serializedConst.description = FindDescription(lines, j);
                        serializedClass.consts.Add(serializedConst);
                    }
                    else if (j < (lines.Count) && !lines[j].StartsWith("/"))
                    {

                        string elementName = lines[j].Split(" ", StringSplitOptions.RemoveEmptyEntries)[1];
                        if (fieldsNames.Contains(elementName))
                        {
                            SerializedField serializedField = new SerializedField();
                            serializedField.name = elementName;
                            serializedField.type = lines[j].Split(" ", StringSplitOptions.RemoveEmptyEntries)[0];
                            serializedField.description = FindDescription(lines, j);
                            serializedClass.fields.Add(serializedField);
                        }
                        else if (propertiesNames.Contains(elementName))
                        {
                            SerializedProperty serializedProperty = new SerializedProperty();
                            serializedProperty.name = elementName;
                            serializedProperty.type = lines[j].Split(" ", StringSplitOptions.RemoveEmptyEntries)[0];
                            serializedProperty.description = FindDescription(lines, j);
                            serializedClass.properties.Add(serializedProperty);
                        }
                        else
                        {
                            var splitForCheckMethods = elementName.Split("(", StringSplitOptions.RemoveEmptyEntries);
                            if (splitForCheckMethods.Count() > 1 && methodsNames.Contains(splitForCheckMethods[0]))
                            {
                                SerializedMethod serializedMethod = FindDataForMethod(lines, j);
                                serializedMethod.name = lines[j];
                                serializedClass.methods.Add(serializedMethod);
                            }
                        }
                        
                    }
                }

                serializedClasses.Add(serializedClass);
            }
        }

        return serializedClasses;
    }
    private string FindDescription(List<string> lines, int lineIndex)
    {
        string description = "";

        if (lineIndex > 1)
        {
            if (lines[lineIndex - 1] == "/e" || lines[lineIndex - 1].StartsWith("/rm"))
            {
                for (int j = lineIndex - 1; j > 0 && lines[j] != "/s"; j--)
                {
                    
                    if (lines[j].StartsWith("/rm"))
                    {
                        description += "Remark:" + lines[j].Replace("/rm", "") + " ";
                    }
                    else if (!lines[j].StartsWith("/e"))
                    {
                        description += lines[j];
                    }
                }
            }
        }
            return description;
    }
    private SerializedMethod FindDataForMethod(List<string> lines, int lineIndex)
    {
        SerializedMethod serializedMethod = new SerializedMethod();
        serializedMethod.parameters = new List<SerializedMethodParam>();
        string description = "";

        if (lineIndex > 1)
        {
            if (lines[lineIndex - 1].StartsWith("/r") || lines[lineIndex - 1].StartsWith("/e") || lines[lineIndex - 1].StartsWith("/p"))
            {
                for (int j = lineIndex - 1; j > 0 && lines[j] != "/s"; j--)
                {
                    if (lines[j].StartsWith("/rt"))
                    {
                        serializedMethod.retunrs = lines[j].Replace("/rt", "");
                    }
                    else if (lines[j].StartsWith("/rm"))
                    {
                        description += "Remark:" + lines[j].Replace("/rm", "") + " ";
                    }
                    else if (lines[j].StartsWith("/p"))
                    {
                        SerializedMethodParam serializedMethodParam = new SerializedMethodParam();
                        var paramSplit = lines[j].Replace("/p", "").Split("/s");
                        serializedMethodParam.name = paramSplit[0];
                        if(paramSplit.Count() > 1)
                        {
                            serializedMethodParam.description = paramSplit[1];
                        }
                        serializedMethod.parameters.Add(serializedMethodParam);
                    }
                    else if (!lines[j].StartsWith("/e"))
                    {
                        description += lines[j];
                    }
                }
            }
        }
        serializedMethod.description = description;
        return serializedMethod;
    }
    [Serializable]
    public struct SerializedClass 
    {
        public string name;
        public string description;
        public List<SerializedConst> consts;
        public List<SerializedField> fields;
        public List<SerializedProperty> properties;
        public List<SerializedMethod> methods;
    }
    [Serializable]
    public struct SerializedConst
    {
        public string name;
        public string description;
        public string type;
        public string value;
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