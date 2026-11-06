using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace org_technika
{
    public partial class MainWindow : Window
    {
        private readonly ApiClient _api = new ApiClient();
        private readonly SessionState _session = new SessionState();
        private JToken _selectedOrder;
        private bool _busy;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += async (sender, args) =>
            {
                await Task.Delay(1400);
                LoadingPanel.Visibility = Visibility.Collapsed;
                await CheckApiAsync();
            };
        }

        private async Task CheckApiAsync()
        {
            try { await _api.GetAsync("api/values", null); FooterStatusText.Text = "API доступен."; }
            catch (Exception ex) { FooterStatusText.Text = "API недоступен. Запустите проект API и проверьте порт 62236. " + ex.Message; }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            await RunBusyAsync(LoginButton, async () =>
            {
                try
                {
                    JToken result = await _api.LoginAsync(LoginTextBox.Text, PasswordBox.Password);
                    _session.Token = (string)result["AccessToken"];
                    _session.Login = (string)result["Login"];
                    _session.Role = (string)result["Role"];
                    _session.UserId = (int?)result["UserId"] ?? 0;
                    _session.ExpiresAtUtc = (DateTime?)result["ExpiresAtUtc"] ?? DateTime.UtcNow.AddMinutes(30);
                    LoginPanel.Visibility = Visibility.Collapsed;
                    ApplicationPanel.Visibility = Visibility.Visible;
                    CurrentUserText.Text = string.Format("Пользователь: {0} | Роль: {1}", _session.Login, _session.Role);
                    ConfigureRole();
                    await RefreshOrdersAsync();
                }
                catch (Exception ex) { LoginStatusText.Text = ex.Message; }
            });
        }

        private void ConfigureRole()
        {
            bool admin = _session.Role == "Администратор";
            bool receptionist = _session.Role == "Приемщик";
            bool master = _session.Role == "Мастер";
            ClientsTab.Visibility = admin ? Visibility.Visible : Visibility.Collapsed;
            MastersTab.Visibility = admin ? Visibility.Visible : Visibility.Collapsed;
            PartsTab.Visibility = admin ? Visibility.Visible : Visibility.Collapsed;
            ReportsTab.Visibility = admin ? Visibility.Visible : Visibility.Collapsed;
            ArchiveTab.Visibility = admin ? Visibility.Visible : Visibility.Collapsed;
            CreateOrderButton.Visibility = admin ? Visibility.Visible : Visibility.Collapsed;
            SaveDiagnosisButton.Visibility = master ? Visibility.Visible : Visibility.Collapsed;
            ChangeStatusButton.Visibility = admin || receptionist || master ? Visibility.Visible : Visibility.Collapsed;
            PayOrderButton.Visibility = admin ? Visibility.Visible : Visibility.Collapsed;
            ArchiveOrderButton.Visibility = admin ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            _session.Token = null; ApplicationPanel.Visibility = Visibility.Collapsed; LoginPanel.Visibility = Visibility.Visible;
            PasswordBox.Clear(); LoginStatusText.Text = string.Empty;
        }

        private async void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source != MainTabs || !_session.IsAuthenticated) return;
            if (ClientsTab.IsSelected) await RefreshClientsAsync();
            if (MastersTab.IsSelected) await RefreshMastersAsync();
            if (PartsTab.IsSelected) await RefreshPartsAsync();
            if (ArchiveTab.IsSelected) await RefreshArchiveAsync();
        }

        private async Task RefreshOrdersAsync()
        {
            try
            {
                string query = _session.Role == "Мастер" ? "api/master/orders" : "api/orders";
                List<string> filters = new List<string>();
                if (_session.Role != "Мастер" && OrderFromDatePicker.SelectedDate.HasValue) filters.Add("dateFrom=" + OrderFromDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd"));
                if (_session.Role != "Мастер" && OrderToDatePicker.SelectedDate.HasValue) filters.Add("dateTo=" + OrderToDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd"));
                if (filters.Count > 0) query += "?" + string.Join("&", filters);
                JArray rows = (JArray)await _api.GetAsync(query, _session.Token);
                string search = OrderSearchTextBox.Text.Trim();
                OrdersGrid.ItemsSource = rows.Where(x => string.IsNullOrWhiteSpace(search) || ((string)x["OrderNumber"] ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || ((string)x["ClientName"] ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                FooterStatusText.Text = "Загружено заказов: " + OrdersGrid.Items.Count;
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private async void RefreshOrdersButton_Click(object sender, RoutedEventArgs e) { await RefreshOrdersAsync(); }
        private async void RefreshArchiveButton_Click(object sender, RoutedEventArgs e) { await RefreshArchiveAsync(); }
        private async Task RefreshArchiveAsync() { try { ArchiveGrid.ItemsSource = await _api.GetAsync("api/orders/archive", _session.Token); } catch (Exception ex) { ShowError(ex); } }
        private async void OrderSearchTextBox_TextChanged(object sender, TextChangedEventArgs e) { if (_session.IsAuthenticated) await RefreshOrdersAsync(); }

        private async void OrdersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            JToken row = OrdersGrid.SelectedItem as JToken; if (row == null) return;
            _selectedOrder = row; OrderDetailsTab.IsEnabled = true; MainTabs.SelectedItem = OrderDetailsTab;
            try
            {
                string orderPath = _session.Role == "Мастер" ? "api/master/orders/" : "api/orders/";
                JToken order = await _api.GetAsync(orderPath + (int)row["OrderId"], _session.Token);
                OrderTitleText.Text = "Заказ " + (string)order["OrderNumber"];
                JObject client = (JObject)order["Client"]; JObject equipment = (JObject)order["Equipment"];
                OrderClientText.Text = client == null
                    ? "Клиент: " + ((string)order["ClientName"] ?? "не указан") + ", " + ((string)order["ClientPhone"] ?? "")
                    : "Клиент: " + (string)client["FullName"] + ", " + (string)client["Phone"];
                OrderEquipmentText.Text = "Оборудование: " + (string)(equipment?["TypeName"] ?? order["EquipmentType"]) + " " + (string)(equipment?["ModelName"] ?? order["ModelName"]) + ", S/N " + (string)(equipment?["SerialNumber"] ?? order["SerialNumber"]);
                JObject master = (JObject)order["Master"];
                OrderStatusText.Text = "Статус: " + (string)order["StatusName"] + " | Мастер: " + ((string)master?["FullName"] ?? "назначен");
                OrderFaultText.Text = (string)order["FaultDescription"]; DiagnosisTextBox.Text = (string)order["Diagnosis"] ?? string.Empty;
                WorksGrid.ItemsSource = order["Works"]; PartsGrid.ItemsSource = order["Parts"]; OrderTotalText.Text = "Итого: " + (decimal?)order["TotalCost"] + " руб.";
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private async void SaveDiagnosisButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedOrder == null) return;
            if (string.IsNullOrWhiteSpace(DiagnosisTextBox.Text)) { MessageBox.Show("Диагноз не может быть пустым.", "Проверка данных", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            await RunBusyAsync(SaveDiagnosisButton, async () => { await _api.SendJsonAsync(HttpMethod.Put, "api/master/orders/" + (int)_selectedOrder["OrderId"] + "/diagnosis", new { Diagnosis = DiagnosisTextBox.Text }, _session.Token); await RefreshOrdersAsync(); });
        }
        private async void ChangeStatusButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedOrder == null) return;
            try
            {
                JArray statuses = (JArray)await _api.GetAsync("api/order-statuses", _session.Token);
                string status = SelectStatus(statuses); if (string.IsNullOrWhiteSpace(status)) return;
                await RunBusyAsync(ChangeStatusButton, async () => { await _api.SendJsonAsync(new HttpMethod("PATCH"), (_session.Role == "Мастер" ? "api/orders/" : "api/orders/") + (int)_selectedOrder["OrderId"] + "/status", new { StatusName = status }, _session.Token); await RefreshOrdersAsync(); });
            }
            catch (Exception ex) { ShowError(ex); }
        }
        private async void PayOrderButton_Click(object sender, RoutedEventArgs e) { await ConfirmAndSendAsync("Отметить заказ оплаченным?", HttpMethod.Post, "/payment"); }
        private async void ArchiveOrderButton_Click(object sender, RoutedEventArgs e) { await ConfirmAndSendAsync("Архивировать заказ?", HttpMethod.Delete, ""); }
        private async Task ConfirmAndSendAsync(string text, HttpMethod method, string suffix)
        {
            if (_selectedOrder == null || MessageBox.Show(text, "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await RunBusyAsync(null, async () => { await _api.SendJsonAsync(method, "api/orders/" + (int)_selectedOrder["OrderId"] + suffix, new { }, _session.Token); await RefreshOrdersAsync(); });
        }

        private async Task RefreshClientsAsync() { try { ClientsGrid.ItemsSource = await _api.GetAsync("api/clients?search=" + Uri.EscapeDataString(ClientSearchTextBox.Text.Trim()), _session.Token); } catch (Exception ex) { ShowError(ex); } }
        private async Task RefreshMastersAsync() { try { MastersGrid.ItemsSource = await _api.GetAsync("api/masters", _session.Token); } catch (Exception ex) { ShowError(ex); } }
        private async Task RefreshPartsAsync() { try { PartsCatalogGrid.ItemsSource = await _api.GetAsync("api/spare-parts", _session.Token); } catch (Exception ex) { ShowError(ex); } }
        private async void RefreshClientsButton_Click(object sender, RoutedEventArgs e) { await RefreshClientsAsync(); }
        private async void AddClientButton_Click(object sender, RoutedEventArgs e) { string name = Prompt("Клиент", "ФИО клиента:"); if (!string.IsNullOrWhiteSpace(name)) { await _api.SendJsonAsync(HttpMethod.Post, "api/clients", new { FullName = name, Phone = Prompt("Телефон", "Телефон:"), Email = Prompt("Email", "Email:"), Address = Prompt("Адрес", "Адрес:") }, _session.Token); await RefreshClientsAsync(); } }
        private async void AddMasterButton_Click(object sender, RoutedEventArgs e) { string name = Prompt("Мастер", "ФИО мастера:"); if (!string.IsNullOrWhiteSpace(name)) { await _api.SendJsonAsync(HttpMethod.Post, "api/masters", new { FullName = name, Phone = Prompt("Телефон", "Телефон:"), Specialization = Prompt("Специализация", "Специализация:") }, _session.Token); await RefreshMastersAsync(); } }
        private async void AddPartButton_Click(object sender, RoutedEventArgs e) { string name = Prompt("Запчасть", "Название:"); decimal price; decimal stock; if (!string.IsNullOrWhiteSpace(name) && decimal.TryParse(Prompt("Цена", "Цена:"), out price) && decimal.TryParse(Prompt("Остаток", "Количество на складе:"), out stock) && stock >= 0) { await _api.SendJsonAsync(HttpMethod.Post, "api/spare-parts", new { PartName = name, Unit = Prompt("Единица", "Единица измерения:"), UnitPrice = price, StockQuantity = stock }, _session.Token); await RefreshPartsAsync(); } else MessageBox.Show("Название, цена и неотрицательный остаток обязательны.", "Проверка данных", MessageBoxButton.OK, MessageBoxImage.Warning); }

        private async void EditClientButton_Click(object sender, RoutedEventArgs e) { JObject row = ClientsGrid.SelectedItem as JObject; if (row == null) { ShowSelectRowMessage(); return; } JObject value = EditRecord("Изменение клиента", new[] { "ФИО", "Телефон", "Email", "Адрес" }, new[] { (string)row["FullName"], (string)row["Phone"], (string)row["Email"], (string)row["Address"] }); if (value != null) await RunBusyAsync(null, async () => { await _api.SendJsonAsync(HttpMethod.Put, "api/clients/" + (int)row["ClientId"], new { FullName = (string)value["ФИО"], Phone = (string)value["Телефон"], Email = (string)value["Email"], Address = (string)value["Адрес"] }, _session.Token); await RefreshClientsAsync(); }); }
        private async void EditMasterButton_Click(object sender, RoutedEventArgs e) { JObject row = MastersGrid.SelectedItem as JObject; if (row == null) { ShowSelectRowMessage(); return; } JObject value = EditRecord("Изменение мастера", new[] { "ФИО", "Телефон", "Специализация" }, new[] { (string)row["FullName"], (string)row["Phone"], (string)row["Specialization"] }); if (value != null) await RunBusyAsync(null, async () => { await _api.SendJsonAsync(HttpMethod.Put, "api/masters/" + (int)row["MasterId"], new { FullName = (string)value["ФИО"], Phone = (string)value["Телефон"], Specialization = (string)value["Специализация"] }, _session.Token); await RefreshMastersAsync(); }); }
        private async void EditPartButton_Click(object sender, RoutedEventArgs e) { JObject row = PartsCatalogGrid.SelectedItem as JObject; if (row == null) { ShowSelectRowMessage(); return; } JObject value = EditRecord("Изменение запчасти", new[] { "Название", "Единица", "Цена" }, new[] { (string)row["PartName"], (string)row["Unit"], Convert.ToString(row["UnitPrice"]) }); decimal price; if (value != null && decimal.TryParse((string)value["Цена"], out price)) await RunBusyAsync(null, async () => { await _api.SendJsonAsync(HttpMethod.Put, "api/spare-parts/" + (int)row["SparePartId"], new { PartName = (string)value["Название"], Unit = (string)value["Единица"], UnitPrice = price }, _session.Token); await RefreshPartsAsync(); }); }
        private async void DeleteClientButton_Click(object sender, RoutedEventArgs e) { JObject row = ClientsGrid.SelectedItem as JObject; if (row == null) { ShowSelectRowMessage(); return; } await DeleteRecordAsync("Удалить клиента?", "api/clients/" + (int)row["ClientId"], RefreshClientsAsync); }
        private async void DeleteMasterButton_Click(object sender, RoutedEventArgs e) { JObject row = MastersGrid.SelectedItem as JObject; if (row == null) { ShowSelectRowMessage(); return; } await DeleteRecordAsync("Удалить мастера? История заказов сохранится.", "api/masters/" + (int)row["MasterId"], RefreshMastersAsync); }
        private async void DeletePartButton_Click(object sender, RoutedEventArgs e) { JObject row = PartsCatalogGrid.SelectedItem as JObject; if (row == null) { ShowSelectRowMessage(); return; } await DeleteRecordAsync("Удалить запчасть?", "api/spare-parts/" + (int)row["SparePartId"], RefreshPartsAsync); }
        private async Task DeleteRecordAsync(string text, string path, Func<Task> refresh) { if (MessageBox.Show(text, "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; await RunBusyAsync(null, async () => { await _api.SendJsonAsync(HttpMethod.Delete, path, new { }, _session.Token); await refresh(); }); }
        private static void ShowSelectRowMessage() { MessageBox.Show("Сначала выберите запись в таблице.", "Нет выбранной записи", MessageBoxButton.OK, MessageBoxImage.Information); }
        private static JObject EditRecord(string title, string[] labels, string[] values)
        {
            Window window = new Window { Title = title, Width = 420, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Application.Current.MainWindow, ResizeMode = ResizeMode.NoResize };
            StackPanel panel = new StackPanel { Margin = new Thickness(14) }; List<TextBox> fields = new List<TextBox>();
            for (int i = 0; i < labels.Length; i++) { panel.Children.Add(new TextBlock { Text = labels[i], Margin = new Thickness(0, 4, 0, 2) }); TextBox field = new TextBox { Text = values[i] ?? string.Empty, MinWidth = 360 }; panel.Children.Add(field); fields.Add(field); }
            WrapPanel buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) }; Button ok = new Button { Content = "Ок", Width = 80, IsDefault = true }; Button cancel = new Button { Content = "Отмена", Width = 80, Margin = new Thickness(8, 0, 0, 0), IsCancel = true }; ok.Click += (s, e) => window.DialogResult = true; buttons.Children.Add(ok); buttons.Children.Add(cancel); panel.Children.Add(buttons); window.Content = panel;
            if (window.ShowDialog() != true) return null; JObject result = new JObject(); for (int i = 0; i < labels.Length; i++) result[labels[i]] = fields[i].Text.Trim(); return result;
        }
        private static string SelectStatus(JArray statuses) { Window window = new Window { Title = "Новый статус", Width = 360, Height = 150, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Application.Current.MainWindow }; StackPanel panel = new StackPanel { Margin = new Thickness(12) }; panel.Children.Add(new TextBlock { Text = "Выберите новый статус:" }); ComboBox combo = new ComboBox { Margin = new Thickness(0, 8, 0, 10), ItemsSource = statuses.Select(s => (string)s["StatusName"]).ToList(), SelectedIndex = 0 }; panel.Children.Add(combo); Button ok = new Button { Content = "Ок", Width = 70, HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true }; ok.Click += (s, e) => window.DialogResult = true; panel.Children.Add(ok); window.Content = panel; return window.ShowDialog() == true ? combo.SelectedItem as string : null; }
        private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e) { string[] names = { "ClientId", "FullName", "Phone", "Email", "Address", "CreatedAt", "MasterId", "Specialization", "SparePartId", "PartName", "Unit", "UnitPrice" }; string[] headers = { "ID клиента", "ФИО", "Телефон", "Электронная почта", "Адрес", "Дата создания", "ID мастера", "Специализация", "ID запчасти", "Название", "Единица", "Цена за единицу" }; int index = Array.IndexOf(names, e.PropertyName); if (index >= 0) e.Column.Header = headers[index]; }
        private async void CreateOrderButton_Click(object sender, RoutedEventArgs e)
        {
            int clientId;
            int equipmentId;
            int masterId;
            string number = Prompt("Новый заказ", "Номер заказа:");
            if (string.IsNullOrWhiteSpace(number) || !int.TryParse(Prompt("Клиент", "ID клиента:"), out clientId) ||
                !int.TryParse(Prompt("Оборудование", "ID оборудования:"), out equipmentId))
            {
                MessageBox.Show("Номер, ID клиента и ID оборудования обязательны.", "Проверка данных", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string masterText = Prompt("Мастер", "ID мастера (необязательно):");
            int? selectedMaster = int.TryParse(masterText, out masterId) ? (int?)masterId : null;
            string fault = Prompt("Неисправность", "Описание неисправности:");
            if (string.IsNullOrWhiteSpace(fault)) return;

            await RunBusyAsync(CreateOrderButton, async () =>
            {
                await _api.SendJsonAsync(HttpMethod.Post, "api/orders", new
                {
                    OrderNumber = number,
                    ClientId = clientId,
                    EquipmentId = equipmentId,
                    MasterId = selectedMaster,
                    FaultDescription = fault
                }, _session.Token);
                await RefreshOrdersAsync();
            });
        }
        private async void RevenueReportButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadReportAsync("api/reports/sales", token =>
                "Заказов: " + token["OrdersCount"] +
                "; начислено: " + token["AccruedTotal"] + " руб.; оплачено: " + token["PaidTotal"] + " руб.");
        }

        private async void WorkloadReportButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadReportAsync("api/reports/workload", token =>
            {
                JArray items = (JArray)token["Items"];
                if (items == null || items.Count == 0) return "Активных мастеров за выбранный период нет.";
                return string.Join(Environment.NewLine, items.Select(item =>
                    (string)item["FullName"] + ": заказов — " + item["OrdersCount"] +
                    ", завершено — " + item["CompletedCount"] + ", сумма — " + item["Amount"] + " руб."));
            });
        }

        private async Task LoadReportAsync(string endpoint, Func<JObject, string> formatter)
        {
            try
            {
                List<string> filters = new List<string>();
                if (ReportFromDatePicker.SelectedDate.HasValue) filters.Add("dateFrom=" + ReportFromDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd"));
                if (ReportToDatePicker.SelectedDate.HasValue) filters.Add("dateTo=" + ReportToDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd"));
                if (filters.Count > 0) endpoint += "?" + string.Join("&", filters);
                ReportText.Text = formatter((JObject)await _api.GetAsync(endpoint, _session.Token));
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private static string Prompt(string title, string label)
        {
            Window window = new Window { Title = title, Width = 360, Height = 150, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Application.Current.MainWindow };
            StackPanel panel = new StackPanel { Margin = new Thickness(12) }; panel.Children.Add(new TextBlock { Text = label }); TextBox input = new TextBox { Margin = new Thickness(0, 8, 0, 8) }; panel.Children.Add(input); Button ok = new Button { Content = "OK", Width = 70, HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true }; ok.Click += (s, e) => window.DialogResult = true; panel.Children.Add(ok); window.Content = panel; return window.ShowDialog() == true ? input.Text.Trim() : null;
        }
        private void ShowError(Exception ex) { MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        private async Task RunBusyAsync(Button button, Func<Task> action)
        {
            if (_busy) return; _busy = true; if (button != null) button.IsEnabled = false;
            try { await action(); } catch (Exception ex) { ShowError(ex); } finally { if (button != null) button.IsEnabled = true; _busy = false; }
        }
    }
}
