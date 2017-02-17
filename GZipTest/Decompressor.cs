using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipTest
{
    class Decompressor
    {

        private string input_file_path;
        private string output_file_path;

        //Кол-во побочных потоков
        private int thread_counter = Environment.ProcessorCount;

        //Размер буфера
        private int bufferSize = 1 * 1024 * 1024;

        //Хранение данных
        private byte[][] data_parts;
        private byte[][] gz_data_parts;

        public Decompressor(string fin, string fout)
        {
            input_file_path = fin;
            output_file_path = fout;
            data_parts = new byte[thread_counter][];
            gz_data_parts = new byte[thread_counter][];
        }


        public void Decompress()
        {
            using (FileStream in_stream = new FileStream(input_file_path, FileMode.Open))
            {
                using (FileStream out_stream = new FileStream(output_file_path, FileMode.Create))
                {

                    byte[] block_size = new byte[4];
                    int size_of_part;

                    Thread[] thread_array = new Thread[thread_counter];

                    Console.WriteLine("Processing... ");
                    double progress;

                    while (in_stream.Position < in_stream.Length)
                    {

                        Console.SetCursorPosition(0, Console.CursorTop);
                        progress = in_stream.Position * 100 / in_stream.Length;
                        progress = Math.Round(progress, 2); ;
                        Console.Write(progress + "%");

                        for (int part = 0; part < thread_counter; part++)
                        {
                            //Считываем размер сжатого блока
                            in_stream.Read(block_size, 0, 4);

                            //Конвертируем
                            size_of_part = BitConverter.ToInt32(block_size, 0);

                            //Считаем блок
                            data_parts[part] = new byte[size_of_part];
                            in_stream.Read(data_parts[part], 0, size_of_part);

                            thread_array[part] = new Thread(PartDecompressor);
                            thread_array[part].Start(part);

                            if (in_stream.Position >= in_stream.Length)
                            {
                                thread_counter = part + 1;
                                break;
                            }
                        }



                        for (int part = 0; part < thread_counter; part++)
                        {
                            thread_array[part].Join();
                            out_stream.Write(gz_data_parts[part], 0, gz_data_parts[part].Length);
                        }
                    }
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nFile decompressed");
            Console.ResetColor();
        }

        private void PartDecompressor(object i)
        {

            int num = (int)i;
            int size = 0;
            byte[] buf = new byte[bufferSize];
            try
            {
                using (MemoryStream tmp_stream = new MemoryStream())
                {
                    using (GZipStream zip_stream = new GZipStream(new MemoryStream(data_parts[num]), CompressionMode.Decompress))
                    {

                        //Получаем число распакованных байтов
                        size = zip_stream.Read(buf, 0, bufferSize);

                        //Пишем в поток
                        tmp_stream.Write(buf, 0, size);

                    }
                    gz_data_parts[num] = tmp_stream.ToArray();
                }
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                Console.ResetColor();
            }
        }


    }
}
