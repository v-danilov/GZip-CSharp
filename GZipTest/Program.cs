using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;




namespace GZipTest
{
    class Program
    {
        static int Main(string[] args)
        {


            //Приветственное сообщение
            Console.WriteLine("For zipping a file write:     compress [input file name] [zip name] ");
            Console.WriteLine("For unzupping a file write:   decompress [zip name] [output file name]");
            string message;
            string[] mes_split;

            while (true)
            {
                Console.Write(">");

                //Считываем команду
                message = Console.ReadLine();

                //Разбиваем 
                mes_split = message.Split(' ');

                //Проверяем синтаксис команды
                if (mes_split.Length == 3)
                {
                    string input_file = mes_split[1];
                    string output_file = mes_split[2];


                    if (File.Exists(input_file) && output_file != "")
                    {
                        try
                        {
                            switch (mes_split[0])
                            {
                                case "compress":

                                    MultiCompressor mcomp = new MultiCompressor(input_file, output_file);
                                    mcomp.Compress();

                                    break;

                                case "decompress":

                                    MultiDecompressor decomp = new MultiDecompressor(input_file, output_file);
                                    decomp.Deompress();

                                    break;

                                default:
                                    print_error("Unsupported command");
                                    break;

                            }
                        }
                        catch (Exception e)
                        {
                            print_error(e.Message);
                            print_error(e.StackTrace);
                            return 1;
                        }
                    }
                    else
                    {
                        print_error("Check file paths");
                    }
                }
                else
                {
                    print_error("Wrong syntax");
                }
            }
        }

        static void print_error(string err)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(err);
            Console.ResetColor();
        }
    }
}
