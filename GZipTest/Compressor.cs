using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;


namespace GZipTest
{
    class Compressor
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

        public Compressor(string fin, string fout)
        {
            input_file_path = fin;
            output_file_path = fout;
            data_parts = new byte[thread_counter][];
            gz_data_parts = new byte[thread_counter][];
        }

        public void Compress()
        {
            using (FileStream in_stream = new FileStream(input_file_path, FileMode.Open))
            {
                using (FileStream out_stream = new FileStream(output_file_path, FileMode.Create))
                {
                    int size_of_part;
                    Thread[] thread_array = new Thread[thread_counter];

                    Console.WriteLine("Processing... ");
                    double progress;

                    //Начинаем чтение
                    while (in_stream.Position < in_stream.Length)
                    {
                        Console.SetCursorPosition(0, Console.CursorTop);
                        progress = in_stream.Position * 100 / in_stream.Length;
                        progress = Math.Round(progress, 2); ;
                        Console.Write(progress + "%");

                        //Разбиваем на кол-во частей = кол-ву потоков
                        for (int part = 0; part < thread_counter; part++)
                        {
                            
                            //size_of_part = (int)(in_stream.Length - in_stream.Position);
                            long tmp = in_stream.Length - in_stream.Position;

                            //Если размер части > размера буффера
                            if (tmp > bufferSize)
                            {
                                size_of_part = bufferSize;

                            }
                            else
                            {
                                size_of_part = (int)tmp;
                            }

                            //Подготавливаем массив для чтения части
                            data_parts[part] = new byte[size_of_part];

                            //Считываем часть
                            in_stream.Read(data_parts[part], 0, size_of_part);

                            //Сжимаем
                            thread_array[part] = new Thread(PartCompressor);
                            thread_array[part].Start(part);

                            //Отсекаем потоки, сжимающие 0б
                            if ((size_of_part) < bufferSize)
                            {
                                thread_counter = part + 1;
                                break;

                            }

                        }

                        for (int part = 0; part < thread_counter; part++)
                        {
                            //Ждем завершения потока, если он еще не закончил работу
                            thread_array[part].Join();

                            //Получаем размер сжатого блока
                            int l = gz_data_parts[part].Length;

                            //Конвертируем в байты
                            byte[] part_size = BitConverter.GetBytes(l);

                            //Подготавливаем массив для записи размеров блока и его содержимого
                            byte[] data_to_write = new byte[l + 4];

                            //Пишем длину
                            part_size.CopyTo(data_to_write, 0);

                            //И данные
                            gz_data_parts[part].CopyTo(data_to_write, part_size.Length);




                            //Функция для компресси файла без записи длины сжатого блока.
                            //out_stream.Write(gz_data_parts[part], 0, gz_data_parts[part].Length);

                            //Записываем в файл
                            out_stream.Write(data_to_write, 0, data_to_write.Length);

                        }
                    }
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nFile compressed");
            Console.ResetColor();
        }

        private void PartCompressor(object i)
        {
            int num = (int)i;
            try
            {
                using (MemoryStream mem_stream = new MemoryStream(data_parts[num].Length))
                {
                    using (GZipStream zip_stream = new GZipStream(mem_stream, CompressionMode.Compress))
                    {
                        zip_stream.Write(data_parts[num], 0, data_parts[num].Length);
                    }

                    gz_data_parts[num] = mem_stream.ToArray();
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
