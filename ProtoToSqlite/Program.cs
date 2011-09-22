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

            createwriter.WriteLine(createSB.ToString());
            createSB.Clear();
            dropwriter.WriteLine(dropSB.ToString());
            dropSB.Clear();
        }

        public static void ReadMessage(string tablename)
        {
            int bracketcount = 0;
            bool primary_set = !setPrimaries;
            string[] words;

            createSB.Append("CREATE TABLE " + package.Replace(";", "").Replace(".", "") + tablename + " (");
            dropSB.Append("DROP TABLE " + package.Replace(";", "").Replace(".", "") + tablename + ";");

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
                        //for now.. might have to make this an additional table later
                        case "repeated":
                            switch (words[1])
                            {
                                case "uint32":
                                    //a bit of speculation here, the first unsigned int of the message is *probably* the key
                                    if (primary_set)
                                        createSB.Append(words[2] + " UNSIGNED INTEGER,");
                                    else
                                    {
                                        createSB.Append(words[2] + " UNSIGNED INTEGER PRIMARY KEY,");
                                        primary_set = true;
                                    }
                                    break;
                                case "int32":
                                case "sint32":
                                case "bool":
                                case "sfixed32":
                                    createSB.Append(words[2] + " INTEGER,");
                                    break;
                                //speculation: if it's unrecognized, probably a reference, probably unsigned int
                                default:
                                    createSB.Append(words[2] + " UNSIGNED INTEGER,");
                                    break;
                            }
                            break;
                        //simple int for now
                        case "enum":
                            //saved word in sqlite
                            if (words[1].ToLower() == "flags")
                                words[1] = "_flags";
                            createSB.Append(words[1] + " INTEGER,");
                            break;
                    }
                }


            } while (bracketcount > 0);

            createSB.Remove(createSB.Length - 1, 1);
            createSB.Append(");");
        }
    }
}
