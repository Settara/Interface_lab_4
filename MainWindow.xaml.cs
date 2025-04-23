using Microsoft.Win32;
using OP10FormApp;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using ClosedXML.Excel;
using System.IO;
using System.Windows.Media;
using Excel = Microsoft.Office.Interop.Excel;
using System.Windows.Controls;
using System;
using System.Windows.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using static ClosedXML.Excel.XLPredefinedFormat;
using DocumentFormat.OpenXml.Bibliography;

namespace KitchenReportForm
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<KitchenItem> KitchenItems { get; set; }

        // Словарь: Название → Код
        public static readonly Dictionary<string, string> NameToCode = new Dictionary<string, string>
        {
        };

        public static readonly Dictionary<string, string> CodeToName = new Dictionary<string, string>
        {
        };

        public ObservableCollection<string> KitchenItemsList { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> KitchenCodesList { get; set; } = new ObservableCollection<string>();

        public MainWindow()
        {
            LoadKitchenItemsFromFiles(); // сначала загрузить списки

            for (int i = 0; i < Math.Min(KitchenItemsList.Count, KitchenCodesList.Count); i++)
            {
                NameToCode[KitchenItemsList[i]] = KitchenCodesList[i];
                CodeToName[KitchenCodesList[i]] = KitchenItemsList[i];
            }

            InitializeComponent();

            KitchenItems = new ObservableCollection<KitchenItem>
            {
                new KitchenItem { Number = 1 }
            };

            DataContext = this;
        }

        private void AddRowButton_Click(object sender, RoutedEventArgs e)
        {
            if (KitchenItems.Count >= 18)
            {
                MessageBox.Show("Нельзя добавить больше 18 строк.");
                return;
            }

            KitchenItems.Add(new KitchenItem { Number = KitchenItems.Count + 1 });
        }

        private void DeleteRowButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = KitchenDataGrid.SelectedItem as KitchenItem;
            if (selectedItem != null)
            {
                KitchenItems.Remove(selectedItem);
            }
            else
            {
                MessageBox.Show("Выберите строку для удаления.");
            }
        }

        private int TryGetInt(IXLCell cell)
        {
            if (cell == null || cell.IsEmpty()) return 0;

            if (cell.DataType == XLDataType.Number)
                return (int)cell.GetDouble();

            if (int.TryParse(cell.GetString(), out int result))
                return result;

            return 0;
        }

        private double TryGetDouble(IXLCell cell)
        {
            if (cell == null || cell.IsEmpty()) return 0.0;

            if (cell.DataType == XLDataType.Number)
                return cell.GetDouble();

            var raw = cell.GetString()
                          .Replace("₽", "")
                          .Replace(" ", "")
                          .Replace(",", ".") // важно: Excel может использовать , вместо .
                          .Trim();

            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                return result;

            return 0.0;
        }

        private void ExportTextBoxesToExcel()
        {
            string templatePath = "TemplateFormOP12.xlsx";

            if (!File.Exists(templatePath))
            {
                MessageBox.Show($"Не найден шаблон Excel по пути: {templatePath}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var textBoxValues = new List<string>();

            void Traverse(DependencyObject parent)
            {
                int count = VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    if (child is TextBox tb)
                        textBoxValues.Add(tb.Text);
                    else
                        Traverse(child);
                }
            }

            Traverse(this);

            var saveFileDialog = new SaveFileDialog
            {
                Title = "Сохранить заполненный файл",
                Filter = "Excel файл (*.xlsx)|*.xlsx",
                FileName = "ФормаОП12_заполненная.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                using (var workbook = new XLWorkbook(templatePath))
                {
                    
                    var worksheet = workbook.Worksheet(1);

                    //Экспорт значений из элементов формы
                    worksheet.Cell("A6").Value = OrganizationTextBox.Text;        //  организация
                    worksheet.Cell("A8").Value = DepartmentTextBox.Text;          //  структурное подразделение
                    worksheet.Cell("U14").Value = DocumentNumberTextBox.Text;     //  Номер документа

                    

                     if (ApprovalDatePicker.SelectedDate is System.DateTime date)
                     {
                         var culture = new CultureInfo("ru-RU");
                         worksheet.Cell("AL17").Value = date.Day.ToString("00");               //  День
                         worksheet.Cell("AN17").Value = date.ToString("MMMM", culture);        //  Месяц текстом (например, "апрель")
                         worksheet.Cell("AU17").Value = date.Year;                             //  Год
                     }


                    worksheet.Cell("AB14").Value = ApprovalDatePicker2.SelectedDate?.ToString("dd.MM.yyyy");  // Дата составления акта

                     worksheet.Cell("AO6").Value = OkpoTextBox.Text;                //  Код по ОКПО
                     worksheet.Cell("AO9").Value = OkdpTextBox.Text;                //  Вид деятельности по ОКДП
                     worksheet.Cell("AO10").Value = OperationTextBox.Text;          //  Вид операции
                     worksheet.Cell("AM13").Value = PositionTextBox.Text;           //  Должность руководителя
                     worksheet.Cell("AQ15").Value = DecryptionSignature.Text;        //  Расшифровка подписи руководителя

                     worksheet.Cell("E93").Value = SpicesPercentTextBox.Text;
                     worksheet.Cell("T93").Value = SpicesRubTextBox.Text;           //  Специи (руб.)
                     worksheet.Cell("AL93").Value = SpicesKopTextBox.Text;          //  Специи (коп.)

                     worksheet.Cell("D95").Value = SaltPercentTextBox.Text;
                     worksheet.Cell("T95").Value = SaltRubTextBox.Text;             //  Соль (руб.)
                     worksheet.Cell("AL95").Value = SaltKopTextBox.Text;            //  Соль (коп.)

                     // Суммируем
                     int spicesRub = 0;
                     int spicesKop = 0;
                     int saltRub = 0;
                     int saltKop = 0;

                     int.TryParse(SpicesRubTextBox.Text, out spicesRub);
                     int.TryParse(SpicesKopTextBox.Text, out spicesKop);
                     int.TryParse(SaltRubTextBox.Text, out saltRub);
                     int.TryParse(SaltKopTextBox.Text, out saltKop);

                     int totalKop = spicesKop + saltKop;
                     int totalRub = spicesRub + saltRub + (totalKop / 100);
                     totalKop = totalKop % 100;

                     // Записываем
                     worksheet.Cell("T97").Value = totalRub; // Итого руб.
                     worksheet.Cell("AL97").Value = totalKop; // Итого коп.


                    // Члены комиссии
                    worksheet.Cell("AC100").Value = BrigadirSignatureDIscription.Text;   //  Бригадир

                    worksheet.Cell("H102").Value = JobTitleComisionTextBox.Text;         //  Должность члена комиссии
                    worksheet.Cell("AC102").Value = JobTitleSignatureDIscription.Text;   //  Расшифровка подписи члена комиссии

                    worksheet.Cell("H104").Value = JobTitleComisionTextBox2.Text;        //  Должность члена комиссии
                    worksheet.Cell("AC104").Value = JobTitleSignatureDIscription2.Text;  //  Расшифровка подписи члена комиссии

                    // Касса
                    worksheet.Cell("A109").Value = CashRubTextBox.Text;                  //  руб.
                    worksheet.Cell("AS109").Value = CashKopTextBox.Text;                 //  коп.

                    worksheet.Cell("O111").Value = CashierSignatureDecryption.Text;      // Кассир
                    worksheet.Cell("U113").Value = AccountantSignatureDecryption.Text;   // Бухгалтер                                                                          



                    // Прямой доступ к ItemsSource
                    var itemsSource = KitchenDataGrid.ItemsSource as IEnumerable<KitchenItem>;
                    if (itemsSource != null)
                    {
                        var items = itemsSource.Take(18).ToList(); // максимум 18 строк

                        for (int i = 0; i < items.Count; i++)
                        {
                            int targetRow = (i < 30) ? 26 + i : 65 + (i - 30);
                            var item = items[i];

                            worksheet.Cell($"A{targetRow}").Value = item.Number;
                            worksheet.Cell($"D{targetRow}").Value = item.CalculationCardNumber;
                            worksheet.Cell($"H{targetRow}").Value = item.Name;
                            worksheet.Cell($"S{targetRow}").Value = item.Code;
                            worksheet.Cell($"V{targetRow}").Value = item.Quantity;

                            worksheet.Cell($"Z{targetRow}").Value = item.PriceFact;
                            worksheet.Cell($"Z{targetRow}").Style.NumberFormat.Format = "#,##0.00 ₽";
                            worksheet.Cell($"AE{targetRow}").Value = item.SumFact;
                            worksheet.Cell($"AE{targetRow}").Style.NumberFormat.Format = "#,##0.00 ₽";


                            worksheet.Cell($"AJ{targetRow}").Value = item.PriceDiscount;
                            worksheet.Cell($"AJ{targetRow}").Style.NumberFormat.Format = "#,##0.00 ₽";
                            worksheet.Cell($"AO{targetRow}").Value = item.SumDiscount;
                            worksheet.Cell($"AO{targetRow}").Style.NumberFormat.Format = "#,##0.00 ₽";

                            worksheet.Cell($"AT{targetRow}").Value = item.Note;

                        }

                    }
                    
                    try
                    {
                        workbook.SaveAs(saveFileDialog.FileName);
                        MessageBox.Show("Файл успешно сохранён:\n" + saveFileDialog.FileName, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (IOException ex)
                    {
                        MessageBox.Show($"Не удалось сохранить файл. Он может быть открыт в другой программе или заблокирован.\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
               
                }

                //MessageBox.Show("Файл успешно сохранён:\n" + saveFileDialog.FileName, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            ExportTextBoxesToExcel();
        }

        private void IntegerOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем только цифры 0–9
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private void IntegerRange0To100_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Предполагаемый полный текст после ввода
            if (sender is TextBox tb)
            {
                string fullText = tb.Text.Insert(tb.SelectionStart, e.Text);
                if (int.TryParse(fullText, out int value))
                {
                    e.Handled = value < 0;
                }
                else
                {
                    e.Handled = true; // не число — отклоняем
                }
            }
        }
        private void IntegerRange0To100_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (int.TryParse(tb.Text, out int value))
                {
                    if (value < 0) tb.Text = "0";
                    //else if (value > 100) tb.Text = "100";
                }
                else
                {
                    tb.Text = "0"; // не число — сбрасываем
                }
            }
        }

        private void IntegerCop_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Предполагаемый полный текст после ввода
            if (sender is TextBox tb)
            {
                string fullText = tb.Text.Insert(tb.SelectionStart, e.Text);
                if (int.TryParse(fullText, out int value))
                {
                    e.Handled = value < 0 || value > 99;
                }
                else
                {
                    e.Handled = true; // не число — отклоняем
                }
            }
        }
        private void IntegerCop_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (int.TryParse(tb.Text, out int value))
                {
                    if (value < 0) tb.Text = "0";
                    else if (value > 99) tb.Text = "99";
                }
                else
                {
                    tb.Text = "0"; // не число — сбрасываем
                }
            }
        }

        private void LoadKitchenItemsFromFiles()
        {
            string itemsPath = "kitchen_items.txt"; // файл с названиями
            string codesPath = "kitchen_codes.txt"; // файл с кодами

            if (File.Exists(itemsPath))
            {
                var items = File.ReadAllLines(itemsPath)
                                .Where(line => !string.IsNullOrWhiteSpace(line))
                                .ToList();
                KitchenItemsList.Clear();
                foreach (var item in items)
                    KitchenItemsList.Add(item);
            }

            if (File.Exists(codesPath))
            {
                var codes = File.ReadAllLines(codesPath)
                                .Where(line => !string.IsNullOrWhiteSpace(line))
                                .ToList();
                KitchenCodesList.Clear();
                foreach (var code in codes)
                    KitchenCodesList.Add(code);
            }
        }


    }

    public class KitchenItem : INotifyPropertyChanged
    {
        private string _name;
        private string _code;
        private string _note;
        private double _priceFact;
        private double _priceDiscont;

        //Номер
        public int Number { get; set; }

        //Номер калькуляционной карточки
        public int CalculationCardNumber { get; set; }

        //Готовое изделие
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));

                    if (MainWindow.NameToCode.TryGetValue(_name, out var matchedCode))
                    {
                        _code = matchedCode;
                        OnPropertyChanged(nameof(Code));
                    }
                }
            }
        }

        //Код
        public string Code
        {
            get => _code;
            set
            {
                if (_code != value)
                {
                    _code = value;
                    OnPropertyChanged(nameof(Code));

                    if (MainWindow.CodeToName.TryGetValue(_code, out var matchedName))
                    {
                        _name = matchedName;
                        OnPropertyChanged(nameof(Name));
                    }
                }
            }
        }

        //Количество
        public double Quantity { get; set; }
        

        //Цена(по ценам фактической реализации)
        public double PriceFact
        {
            get => _priceFact;
            set { _priceFact = value; OnPropertyChanged(nameof(PriceFact)); }
        }

        //Сумма(по ценам фактической реализации)
        public double SumFact { get; set; }

        //Цена(по учетным ценам производства)
        public double PriceDiscount
        {
            get => _priceDiscont;
            set { _priceDiscont = value; OnPropertyChanged(nameof(PriceDiscount)); }
        }

        //Сумма(по учетным ценам производства)
        public double SumDiscount { get; set; }

        //Примечание
        public string Note { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
