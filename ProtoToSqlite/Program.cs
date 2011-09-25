using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ProtoToSqlite
{
    class Program
    {
        static TextReader reader;
        static StringBuilder createSB = new StringBuilder();
        static StringBuilder dropSB = new StringBuilder();
        static bool setPrimaries;
        static string package = "";
        static TextWriter createwriter, dropwriter;
        static List<KeyValuePair<string, string>> extratables = new List<KeyValuePair<string, string>>();

        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "")
            {
                Console.WriteLine("Syntax <directory/file> <optional --noprimaries>");
                return;
            }

            setPrimaries = args.Length < 2 ? true : args[1] != "--noprimaries";


            try
            {
                createwriter = new StreamWriter("Create tables transaction.txt");
                dropwriter = new StreamWriter("Drop tables transaction.txt");

                if (Directory.Exists(args[0]))
                {
                    ReadDirectory(new DirectoryInfo(args[0]));
                }
                else if (File.Exists(args[0]))
                {
                    ReadFile(new FileInfo(args[0]));
                }
                else
                    throw new Exception("no file or directory found");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }

            createwriter.Close();
            dropwriter.Close();
        }

        public static void ReadDirectory(DirectoryInfo di)
        {
            foreach (DirectoryInfo d in di.GetDirectories())
            {
                ReadDirectory(d);
            }

            foreach (FileInfo f in di.GetFiles())
            {
                if (f.Extension == ".proto")
                {
                    ReadFile(f);
                }
            }
        }

        public static void ReadFile(FileInfo fi)
        {
            try
            {
                reader = new StreamReader(fi.FullName);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }

            string[] words;

            while (reader.Peek() != -1)
            {
                words = reader.ReadLine().Split(' ');

                if (words.Length == 0) continue;

                switch (words[0])
                {
                    case "import":
                        break;
                    case "package":
                        package = words[1];
                        break;
                    case "message":
                        ReadMessage(words[1]);
                        break;
                }
            }

            createwriter.Write(Environment.NewLine + Environment.NewLine);
            dropwriter.Write(Environment.NewLine + Environment.NewLine);
        }

        public static void ReadMessage(string tablename)
        {
            int bracketcount = 0;
            string[] words;

            string fulltablename = package.Replace(";", "").Replace(".", "") + tablename;

            createSB.Append("CREATE TABLE " + fulltablename + " (");
            dropSB.Append("DROP TABLE " + fulltablename + ";" + Environment.NewLine);

            //create a row guid
            if (setPrimaries)
                createSB.Append("id GUID NOT NULL PRIMARY KEY,");

            do
            {
                words = reader.ReadLine().Split(' ');

                if (words.Length > 0)
                {
                    switch (words[0])
                    {
                        case "{":
                            bracketcount++;
                            break;
                        case "}":
                            bracketcount--;
                            break;
                        case "required":
                        case "optional":
                            switch (words[1])
                            {
                                case "uint32":
                                case "int32":
                                case "sint32":
                                case "bool":
                                case "sfixed32":
                                    createSB.Append(words[2] + " INTEGER,");
                                    break;
                                //speculation: if it's unrecognized, probably a reference, probably unsigned int
                                default:
                                    createSB.Append(words[2] + " GUID,");
                                    break;
                            }
                            break;
                        case "repeated":
                            //create an additional table to contain the repeater
                            createSB.Append(words[2] + " INTEGER,");
                            switch (words[1])
                            {
                                case "int32":
                                case "sint32":
                                case "uint32":
                                case "sfixed32":
                                    extratables.Add(new KeyValuePair<string, string>(fulltablename + "_" + words[2], "INTEGER"));
                                    break;
                                default:
                                    //probably a reference that already has a table. we can link to its id
                                    break;
                            }
                            break;
                        //not handling enums for now
                        /*case "enum":
                            //saved word in sqlite
                            createSB.Append(words[1] + " INTEGER,");
                            break;*/
                    }
                }


            } while (bracketcount > 0);



            createSB.Remove(createSB.Length - 1, 1);
            createSB.Append(");" + Environment.NewLine);

            foreach (var table in extratables)
            {
                createSB.Append("CREATE TABLE " + table.Key + " (id GUID NOT NULL PRIMARY KEY, refid UNSIGNED INTEGER, value " + table.Value + ");" + Environment.NewLine);
                dropSB.Append("DROP TABLE " + table.Key + ";" + Environment.NewLine);
            }

            extratables.Clear();

            createwriter.Write(createSB.ToString());
            createSB.Clear();
            dropwriter.Write(dropSB.ToString());
            dropSB.Clear();
        }


    }
}
