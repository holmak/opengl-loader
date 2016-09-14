﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;

namespace GLGenerator
{
    class GLCommand
    {
        public string Name;
        public List<string> ParamTypes = new List<string>();
        public List<string> ParamNames = new List<string>();
        public string ReturnType;
    }

    class Program
    {
        static void Main(string[] args)
        {
            XmlDocument document = new XmlDocument();
            document.Load("Registry/gl.xml");

            Dictionary<string, List<string>> groups = new Dictionary<string, List<string>>();
            Dictionary<string, uint> enumValues = new Dictionary<string, uint>();
            List<GLCommand> commands = new List<GLCommand>();
            // TODO: Features
            // TODO: Extensions

            foreach (XmlNode group in document.SelectNodes("/registry/groups/group"))
            {
                List<string> members = group.SelectNodes("enum").Cast<XmlNode>()
                    .Select(x => x.Attributes["name"].Value).ToList();

                groups.Add(group.Attributes["name"].Value, members);
            }

            foreach (XmlNode constant in document.SelectNodes("/registry/enums/enum"))
            {
                string name = constant.Attributes["name"].Value;
                string literal = constant.Attributes["value"].Value;

                // Ignore enum values that are part of other APIs, like GLES.
                XmlNode apiNode = constant.Attributes["api"];
                if (apiNode != null && apiNode.Value != "gl")
                {
                    continue;
                }

                uint value;
                if (constant.Attributes["type"] != null)
                {
                    // Ignore enums with non-default types. (There are a couple of enums with 64-bit values.)
                    continue;
                }
                else if (literal.StartsWith("0x"))
                {
                    try
                    {
                        value = Convert.ToUInt32(literal, 16);
                    }
                    catch (OverflowException)
                    {
                        continue;
                    }
                }
                else if (literal.StartsWith("-"))
                {
                    value = (uint)int.Parse(literal, NumberStyles.Integer);
                }
                else
                {
                    value = uint.Parse(literal, NumberStyles.Integer);
                }

                enumValues.Add(name, value);
            }

            foreach (XmlNode commandNode in document.SelectNodes("/registry/commands/command"))
            {
                GLCommand command = new GLCommand();

                XmlNode prototypeNode = commandNode.SelectSingleNode("proto");
                ParseTypeAndName(prototypeNode, out command.ReturnType, out command.Name);

                foreach (XmlNode paramNode in commandNode.SelectNodes("param"))
                {
                    string type, name;
                    ParseTypeAndName(paramNode, out type, out name);
                    command.ParamTypes.Add(type);
                    command.ParamNames.Add(name);
                }

                commands.Add(command);
            }

            using (StreamWriter output = new StreamWriter("Registry/gl.cs"))
            {
                output.WriteLine("public static class GL");
                output.WriteLine("{");

                foreach (var pair in groups)
                {
                    output.WriteLine("    public enum {0} : uint", pair.Key);
                    output.WriteLine("    {");
                    foreach (string member in pair.Value)
                    {
                        uint value;
                        if (enumValues.TryGetValue(member, out value))
                        {
                            output.WriteLine("        {0} = 0x{1:X8},", member, value);
                        }
                    }
                    output.WriteLine("    }");
                    output.WriteLine();
                }

                foreach (GLCommand command in commands)
                {
                    string delegateName = command.Name.Substring(2);
                    output.Write("    public delegate {0} {1}(", command.ReturnType, delegateName);
                    for (int i = 0; i < command.ParamNames.Count; i++)
                    {
                        string comma = (i < command.ParamNames.Count - 1) ? ", " : "";
                        output.Write("{0} {1}{2}", command.ParamTypes[i], command.ParamNames[i], comma);
                    }
                    output.WriteLine(");");
                    output.WriteLine("    public {0} {1};", delegateName, command.Name);
                    output.WriteLine();
                }

                output.WriteLine("    public static void LoadAll()");
                output.WriteLine("    {");

                foreach (GLCommand command in commands)
                {
                    string delegateName = command.Name.Substring(2);
                    output.WriteLine("        {0} = Marshal.GetDelegateForFunctionPointer<{1}>(SDL.SDL_GL_GetProcAddress(\"{0}\"));",
                        command.Name, delegateName);
                }

                output.WriteLine("    }");

                output.WriteLine("}");
            }
        }

        static void ParseTypeAndName(XmlNode node, out string type, out string name)
        {
            string text = node.InnerText;
            name = node.SelectSingleNode("name").InnerText;
            type = text.Substring(0, text.Length - name.Length).Trim();
        }
    }
}