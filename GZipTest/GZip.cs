using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipTest
{


    class GZip
    {

        //Кол-во побочных потоков
        private static int thread_counter = Environment.ProcessorCount;

        //Размер буфера
        private static int bufferSize = 1 * 1024 * 1024;

        //Хранение данных
        private static byte[][] data_parts = new byte[thread_counter][];
        private static byte[][] gz_data_parts = new byte[thread_counter][];


        

        public static void Compress(string input_file_path, string output_file_path)
        {

            using (FileStream in_stream = new FileStream(input_file_path, FileMode.Open)) 
            {
                using (FileStream out_stream = new FileStream(output_file_path, FileMode.Create)) 
                {
                    int size_of_part;
                    Thread[] thread_array = new Thread[thread_counter];

                    Console.WriteLine("Processing... ");
                    //Начинаем чтение
                    while (in_stream.Position < in_stream.Length)
                    {

                        //Разбиваем на кол-во частей = кол-ву потоков
                        for (int part = 0; part < thread_counter; part++)
                        {

                            size_of_part = (int)(in_stream.Length - in_stream.Position);

                            //Если размер части > размера буффера
                            if ((size_of_part) > bufferSize)
                            {
                                size_of_part = bufferSize;
                               
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
            Console.WriteLine("File compressed");
            Console.ResetColor();
        }

       /*
        * /РАБОТАЕТ НЕКОРРЕКТНО/
        * Функция для декопрессии файла, сжатого без записи размеров блоков.
        * Разжимается с помощью анализа формата zip файла
        * Файл читается по 4 байта и происходит поиск "File footer" для вычисления конца сжатого блока
        * "File footer" содержит поле ISIZE размером 4 байта, в котором хранится размер данных до сжатия
        * Для корректной работы данной функции необходимо знать и исходную длину файла,
        * чтобы верно определить количество точек (концов блоков), а также размер последнего сжатого блока.
        * 
        * В связи с этим было решено записывать длину каждого блока вместе с его данными.
        * Это облегчит процесс декомпресии, а также сократит время выполнения
        * 
        * public static void Decompress(string input_file_path, string output_file_path)
        {
            using (FileStream in_stream = new FileStream(input_file_path, FileMode.Open))
            {
                using (FileStream out_stream = new FileStream(output_file_path, FileMode.Create))
                {


                    Console.WriteLine("Analyzing...");

                  
                        double tmp = in_stream.Length / (bufferSize);
                        double array_size = Math.Round(tmp, 0) + 1; //Кол-во блоков меньше истинного, т.к. файл сжат
                        double[] positions = new double[(int)array_size];

                        byte[] info = new byte[4];
                        int pos = 0;


                    //Если файл больше, чем 1Мб
                    if (in_stream.Length > bufferSize)
                    {
                        int cnt = 0;
                        double temp = 0;
                        
                        //Читаем файл
                        while (in_stream.Position < in_stream.Length)
                        {
                            in_stream.Position = pos;

                            //Ищем 4 байта в которых будет указан несжатый размер данных (1Мб)      
                            in_stream.Read(info, 0, 4);
                            int len = BitConverter.ToInt32(info, 0);

                            //Если найден размер, равный буферу (1Мб), значит это конец сжатого блока
                            //(если это предпоследний блок, то его размер может оказаться меньше 1Мб)
                            if (len == bufferSize)
                            {
                                //Определяем размер части
                                
                                Console.WriteLine(cnt + " | " + in_stream.Position);
                                positions[cnt] = in_stream.Position - temp;
                                temp = in_stream.Position;
                                cnt++;
                            }
                            pos++;

                        }
                    }
                   
                    int size_of_part;
                    
                    
                    Thread[] thread_array = new Thread[thread_counter];
                    in_stream.Position = 0;
                    pos = 0;

                    Console.WriteLine("Unzipping...");
                    while (in_stream.Position < in_stream.Length)
                    {
                        for (int part = 0; part < thread_counter; part++)
                        {

                            if ((in_stream.Length - in_stream.Position) <= bufferSize)
                            {
                                
                                size_of_part = (int)(in_stream.Length - in_stream.Position);
                                Console.WriteLine(size_of_part);
                            }
                            else
                            {
                                size_of_part = (int)positions[pos];
                            }


                                data_parts[part] = new byte[size_of_part];
                                in_stream.Read(data_parts[part], 0, size_of_part);
                                thread_array[part] = new Thread(PartDecompressor);
                                thread_array[part].Start(part);
                            pos++;
                           
                            

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
            Console.WriteLine("File decompressed");
            Console.ResetColor();
        }*/

        public static void PartCompressor(object i)
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
            catch(Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                Console.ResetColor();
            }
        }

        public static void PartDecompressor(object i)
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
            catch(Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                Console.ResetColor();
            }
        }

        public static void Decompress(string input_file_path, string output_file_path)
        {
            using (FileStream in_stream = new FileStream(input_file_path, FileMode.Open))
            {
                using (FileStream out_stream = new FileStream(output_file_path, FileMode.Create))
                {

                    byte[] block_size = new byte[4];
                    int size_of_part;

                    Thread[] thread_array = new Thread[thread_counter];

                    Console.WriteLine("Processing... ");

                    while (in_stream.Position < in_stream.Length)
                    {

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
            Console.WriteLine("File decompressed");
            Console.ResetColor();
        }
    }

}
