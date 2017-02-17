using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Threading;


namespace GZipTest
{
    class MultiCompressor
    {
        //Поток сжатия
        private Thread compression_thread;

        //Поток записи в файл
        private Thread writer_thread;

        ///Входной файл
        private string input_file_path;

        //Выходной файл
        private string output_file_path;

        //Размер буфера
        private const int bufferSize = 1 * 1024 * 1024;

        //Возможной количество потоков
        private int thread_counter;

        //Очередь для хранения частей файла
        private ConcurrentQueue<byte[]> file_parts_queue;

        //Лист для хранения сжатых частей
        private MyCuncurrentLinkedList<PartDataContainer> zip_parts_list;

        //Мютекс
        public Mutex mtx = new Mutex();

        //Флаг завершения чтения
        private bool reading_ended;


        public MultiCompressor(string fin, string fout)
        {
            input_file_path = fin;
            output_file_path = fout;
            file_parts_queue = new ConcurrentQueue<byte[]>();
            zip_parts_list = new MyCuncurrentLinkedList<PartDataContainer>();
            thread_counter = Environment.ProcessorCount;
            reading_ended = false;
        }

        public void Compress()
        {
            using (FileStream in_stream = new FileStream(input_file_path, FileMode.Open))
            {

                Console.WriteLine("Processing...");

                int size_of_part;

                //Запускаем побочные потоки сжатия и записи
                compression_thread = new Thread(ThreadController);
                writer_thread = new Thread(WriterThread);

                //Запускаем поток запси
                compression_thread.Start();
                writer_thread.Start();

                //Начинаем чтение файла
                while (in_stream.Position < in_stream.Length)
                {

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
                    byte[] data = new byte[size_of_part];


                    //Считываем часть
                    in_stream.Read(data, 0, size_of_part);
                    file_parts_queue.Enqueue(data);

                }

                //Поднимает флаг окончания чтения
                reading_ended = true;

                //Ждем завершения побочных потоков
                compression_thread.Join();
                writer_thread.Join();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("File compressed");
                Console.ResetColor();
            }
        }


        private void PartCompressor(object i)
        {
            long num = (long)i;

            byte[] data = null;

            //Обработка очереди
            mtx.WaitOne();
            if (file_parts_queue.Count != 0)
            {
                file_parts_queue.TryDequeue(out data);
            }
            mtx.ReleaseMutex();

            //Если данные не был считаны
            if (data == null)
            {
                //Записываем индекс с нулевой датой, чтобы сохранить последовательность
                PartDataContainer pdc = new PartDataContainer(num, null);
                zip_parts_list.AddLast(pdc);
                return;
            }

            //Если удалось получить данные из очереди
            try
            {
                using (MemoryStream mem_stream = new MemoryStream(data.Length))
                {
                    using (GZipStream zip_stream = new GZipStream(mem_stream, CompressionMode.Compress))
                    {
                        //Сжимаем
                        zip_stream.Write(data, 0, data.Length);
                    }


                    //Получаем размер сжатого блока
                    int l = mem_stream.ToArray().Length;

                    //Конвертируем в байты
                    byte[] part_size = BitConverter.GetBytes(l);

                    //Подготавливаем массив для записи размеров блока и его содержимого
                    byte[] data_to_write = new byte[l + 4];

                    //Пишем длину
                    part_size.CopyTo(data_to_write, 0);

                    mem_stream.ToArray().CopyTo(data_to_write, part_size.Length);

                    //Записываем индекс части и соответствующие данные
                    PartDataContainer pdc = new PartDataContainer(num, data_to_write);

                    zip_parts_list.AddLast(pdc);

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


        /// <summary>
        /// Поток распределения задач
        /// </summary>
        private void ThreadController()
        {
            Thread[] thread_array = new Thread[thread_counter];
            long[] index_array = new long[thread_counter];

            //Индекс части для корректной сборки в архив
            long counter = 0;

            //Основной цикл распределения задач
            while (!reading_ended || !file_parts_queue.IsEmpty)
            {
                //Дополнительная проверка
                if (!file_parts_queue.IsEmpty)
                {
                    for (int i = 0; i < thread_counter; i++)
                    {
                        if (thread_array[i] == null || thread_array[i].ThreadState == ThreadState.Stopped)
                        {
                            thread_array[i] = new Thread(PartCompressor);

                            counter++;

                            //Записываем индекс части, с которой будет работать побочный поток
                            index_array[i] = counter;

                            thread_array[i].Start(counter);
                        }
                    }
                }
                //В случае если чтение началось а данных еще нет
                else
                {
                    //Ждем 50мс
                    Thread.Sleep(50);
                }
            }

            //Если чтение закончилось и очередь пуста, ожидаем завершения потоков сжатия
            for (int i = 0; i < thread_counter; i++)
            {
                thread_array[i].Join();
            }
        }

        private void WriterThread()
        {
            using (FileStream out_stream = new FileStream(output_file_path, FileMode.Create))
            {

                PartDataContainer dc;

                int cnt = 1;
                while (zip_parts_list.Count == 0)
                {
                    Thread.Sleep(50);
                }


                var node = zip_parts_list.First;


                //Основной цикл записи в файл(архив)
                while (!reading_ended
                    || zip_parts_list.Count != 0
                    || compression_thread.ThreadState != ThreadState.Stopped)
                {

                    if (zip_parts_list.Count != 0)
                    {
                        //Если лист был очищен, а после поступили новые сжатые данные
                        if (node == null)
                        {
                            node = zip_parts_list.First;
                        }


                        dc = new PartDataContainer(node.Value.Index, node.Value.Data);

                        //Находим нужный индекс по порядку
                        if (dc.Index == cnt)
                        {

                            //Если данный индекс имеет данные
                            if (dc.Data != null)
                            {
                                //Записываем в файл                              
                                out_stream.Write(dc.Data, 0, dc.Data.Length);
                            }

                            //Меняем индекс для поиска следующей части
                            cnt++;

                            //Удаляем элемент из листа
                            zip_parts_list.Remove(node);

                            //Сдвигаем указатель
                            node = zip_parts_list.First;

                        }
                        else
                        {
                            node = node.Next;
                        }

                    }

                    //Если чтение еще идет, но сжатых данных пока нет
                    else
                    {
                        //Ждем
                        Thread.Sleep(50);
                    }
                }
            }
        }
    }



}
