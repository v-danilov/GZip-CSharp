using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Threading;


namespace GZipTest
{
    class MultiDecompressor
    {
        //Поток восстановления данных
        private Thread decompression_thread;

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
        private MyCuncurrentLinkedList<PartDataContainer> unzip_parts_list;

        //Мютекс
        public Mutex mtx = new Mutex();

        //Флаг завершения чтения
        private bool reading_ended;


        public MultiDecompressor(string fin, string fout)
        {
            input_file_path = fin;
            output_file_path = fout;
            file_parts_queue = new ConcurrentQueue<byte[]>();
            unzip_parts_list = new MyCuncurrentLinkedList<PartDataContainer>();
            thread_counter = Environment.ProcessorCount;
            reading_ended = false;
        }

        public void Deompress()
        {
            using (FileStream in_stream = new FileStream(input_file_path, FileMode.Open))
            {

                Console.WriteLine("Processing...");

                byte[] block_size = new byte[4];
                int size_of_part;

                //Запускаем потоки восстановления и записи
                decompression_thread = new Thread(ThreadController);
                writer_thread = new Thread(WriterThread);

                decompression_thread.Start();
                writer_thread.Start();

                //Начинаем чтение файла
                while (in_stream.Position < in_stream.Length)
                {

                    //Считываем размер сжатого блока
                    in_stream.Read(block_size, 0, 4);

                    //Конвертируем
                    size_of_part = BitConverter.ToInt32(block_size, 0);

                    //Подготавливаем массив для чтения части
                    byte[] data = new byte[size_of_part];


                    //Считываем часть
                    in_stream.Read(data, 0, size_of_part);
                    file_parts_queue.Enqueue(data);

                }

                //Поднимает флаг окончания чтения
                reading_ended = true;

                //Ждем завершения побочных потоков
                decompression_thread.Join();
                writer_thread.Join();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("File decompressed");
                Console.ResetColor();
            }
        }


        private void PartDecompressor(object i)
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
                unzip_parts_list.AddLast(pdc);
                return;
            }

            //Если удалось получить данные из очереди
            int size = 0;
            byte[] buf = new byte[bufferSize];

            try
            {
                using (MemoryStream tmp_stream = new MemoryStream())
                {
                    using (GZipStream zip_stream = new GZipStream(new MemoryStream(data), CompressionMode.Decompress))
                    {
                        //Получаем число распакованных байтов
                        size = zip_stream.Read(buf, 0, bufferSize);

                        //Пишем в поток
                        tmp_stream.Write(buf, 0, size);
                    }


                    //Записываем индекс части и соответствующие данные
                    PartDataContainer pdc = new PartDataContainer(num, tmp_stream.ToArray());

                    unzip_parts_list.AddLast(pdc);

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
                            thread_array[i] = new Thread(PartDecompressor);

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
                while (unzip_parts_list.Count == 0)
                {
                    Thread.Sleep(50);
                }


                var node = unzip_parts_list.First;


                //Основной цикл записи в файл(архив)
                while (!reading_ended
                    || unzip_parts_list.Count != 0
                    || decompression_thread.ThreadState != ThreadState.Stopped)
                {

                    if (unzip_parts_list.Count != 0)
                    {
                        //Если лист был очищен, а после поступили новые сжатые данные
                        if (node == null)
                        {
                            node = unzip_parts_list.First;
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
                            unzip_parts_list.Remove(node);

                            //Сдвигаем указатель
                            node = unzip_parts_list.First;

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
