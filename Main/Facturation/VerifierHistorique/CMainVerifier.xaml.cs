using GestionComerce.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace GestionComerce.Main.Facturation.VerifierHistorique
{
    public partial class CMainVerifier : UserControl
    {
        private User user;
        private MainWindow main;
        private CheckHistory selectedCheck;
        private List<CheckHistory> allChecks;
        private byte[] selectedImageBytes; // Store image bytes

        // TODO: Replace this with your actual Invoice class
        private class InvoiceDisplay
        {
            public int InvoiceID { get; set; }
            public string DisplayText { get; set; }
        }

        public CMainVerifier(User u, MainWindow mainWindow)
        {
            InitializeComponent();
            this.user = u;
            this.main = mainWindow;

            // Initialize the list to avoid null reference
            allChecks = new List<CheckHistory>();

            // Set default date
            dpCheckDate.SelectedDate = DateTime.Now;

            // Load data after everything is initialized
            this.Loaded += CMainVerifier_Loaded;
        }

        private async void CMainVerifier_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                // Load all checks
                CheckHistory checkHistory = new CheckHistory();
                allChecks = await checkHistory.GetAllChecksAsync();

                if (dgChecks != null)
                {
                    dgChecks.ItemsSource = allChecks;
                    UpdateResultsCount(allChecks?.Count ?? 0);
                }

                // Load invoices for combobox
                await LoadInvoices();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors du chargement des données: {ex.Message}",
                    "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadInvoices()
        {
            try
            {
                // Load real invoices from database
                InvoiceRepository invoiceRepo = new InvoiceRepository("");

                var invoices = await invoiceRepo.GetAllInvoicesAsync();

                var invoiceList = invoices.Select(i => new InvoiceDisplay
                {
                    InvoiceID = i.InvoiceID,
                    DisplayText = $"Facture #{i.InvoiceNumber ?? "N/A"} - {i.ClientName ?? "N/A"} - {i.TotalTTC:N2} MAD"
                }).ToList();

                cmbInvoice.ItemsSource = invoiceList;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors du chargement des factures: {ex.Message}\n\nStack: {ex.StackTrace}",
                    "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateResultsCount(int count)
        {
            if (txtResultsCount != null)
            {
                txtResultsCount.Text = count == 0 ? "Aucun chèque trouvé" :
                    count == 1 ? "1 chèque trouvé" :
                    $"{count} chèques trouvés";
            }
        }

        private void UpdateButtonVisibility()
        {
            if (selectedCheck != null)
            {
                // Hide Add button, show Update and Delete buttons
                btnAdd.Visibility = Visibility.Collapsed;
                btnUpdate.Visibility = Visibility.Visible;
                btnDelete.Visibility = Visibility.Visible;
            }
            else
            {
                // Show Add button, hide Update and Delete buttons
                btnAdd.Visibility = Visibility.Visible;
                btnUpdate.Visibility = Visibility.Collapsed;
                btnDelete.Visibility = Visibility.Collapsed;
            }
        }

        private void BrowseImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Tous les fichiers|*.*",
                Title = "Sélectionner une image du chèque"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Read the image file as bytes
                    selectedImageBytes = File.ReadAllBytes(openFileDialog.FileName);
                    txtImagePath.Text = Path.GetFileName(openFileDialog.FileName); // Store only filename

                    MessageBox.Show("Image chargée avec succès!", "Succès",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erreur lors du chargement de l'image: {ex.Message}",
                        "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void AddCheck_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm())
                return;

            try
            {
                CheckHistory newCheck = new CheckHistory
                {
                    CheckReference = txtCheckReference.Text.Trim(),
                    CheckImage = selectedImageBytes, // Save binary data
                    CheckImagePath = txtImagePath.Text.Trim(), // Save filename for reference
                    InvoiceID = (int)cmbInvoice.SelectedValue,
                    CheckAmount = string.IsNullOrEmpty(txtCheckAmount.Text) ? (decimal?)null :
                        decimal.Parse(txtCheckAmount.Text.Trim()),
                    CheckDate = dpCheckDate.SelectedDate ?? DateTime.Now,
                    BankName = txtBankName.Text.Trim(),
                    CheckStatus = (cmbStatus.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "En Attente",
                    Notes = txtNotes.Text.Trim()
                };

                int result = await newCheck.InsertCheckAsync();
                if (result > 0)
                {
                    MessageBox.Show("Chèque ajouté avec succès!", "Succès",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearForm();
                    await LoadData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de l'ajout: {ex.Message}",
                    "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void UpdateCheck_Click(object sender, RoutedEventArgs e)
        {
            if (selectedCheck == null)
            {
                MessageBox.Show("Veuillez sélectionner un chèque à modifier.",
                    "Attention", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ValidateForm())
                return;

            try
            {
                selectedCheck.CheckReference = txtCheckReference.Text.Trim();

                // Update image only if new one was selected
                if (selectedImageBytes != null)
                {
                    selectedCheck.CheckImage = selectedImageBytes;
                    selectedCheck.CheckImagePath = txtImagePath.Text.Trim();
                }

                selectedCheck.InvoiceID = (int)cmbInvoice.SelectedValue;
                selectedCheck.CheckAmount = string.IsNullOrEmpty(txtCheckAmount.Text) ? (decimal?)null :
                    decimal.Parse(txtCheckAmount.Text.Trim());
                selectedCheck.CheckDate = dpCheckDate.SelectedDate ?? DateTime.Now;
                selectedCheck.BankName = txtBankName.Text.Trim();
                selectedCheck.CheckStatus = (cmbStatus.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "En Attente";
                selectedCheck.Notes = txtNotes.Text.Trim();

                int result = await selectedCheck.UpdateCheckAsync();
                if (result > 0)
                {
                    MessageBox.Show("Chèque modifié avec succès!", "Succès",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearForm();
                    await LoadData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la modification: {ex.Message}",
                    "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteCheck_Click(object sender, RoutedEventArgs e)
        {
            if (selectedCheck == null)
            {
                MessageBox.Show("Veuillez sélectionner un chèque à supprimer.",
                    "Attention", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Êtes-vous sûr de vouloir supprimer le chèque '{selectedCheck.CheckReference}'?",
                "Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    int deleteResult = await selectedCheck.DeleteCheckAsync();
                    if (deleteResult > 0)
                    {
                        MessageBox.Show("Chèque supprimé avec succès!", "Succès",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        ClearForm();
                        await LoadData();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erreur lors de la suppression: {ex.Message}",
                        "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RefreshData_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
            _ = LoadData();
        }

        private void ResetForm_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            await PerformSearch();
        }

        private async void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            await PerformSearch();
        }

        private async Task PerformSearch()
        {
            try
            {
                if (allChecks == null)
                    allChecks = new List<CheckHistory>();

                string searchText = txtSearch.Text.Trim();

                if (string.IsNullOrEmpty(searchText))
                {
                    if (dgChecks != null)
                    {
                        dgChecks.ItemsSource = allChecks;
                        UpdateResultsCount(allChecks?.Count ?? 0);
                    }
                }
                else
                {
                    CheckHistory checkHistory = new CheckHistory();
                    var results = await checkHistory.SearchChecksByReferenceAsync(searchText);
                    if (dgChecks != null)
                    {
                        dgChecks.ItemsSource = results;
                        UpdateResultsCount(results?.Count ?? 0);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la recherche: {ex.Message}",
                    "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void FilterStatus_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Avoid running on initialization
            if (!this.IsLoaded || allChecks == null)
                return;

            try
            {
                var selectedItem = (cmbFilterStatus.SelectedItem as ComboBoxItem)?.Content.ToString();

                if (selectedItem == "Tous les statuts" || string.IsNullOrEmpty(selectedItem))
                {
                    if (dgChecks != null)
                    {
                        dgChecks.ItemsSource = allChecks;
                        UpdateResultsCount(allChecks?.Count ?? 0);
                    }
                }
                else
                {
                    CheckHistory checkHistory = new CheckHistory();
                    var results = await checkHistory.GetChecksByStatusAsync(selectedItem);
                    if (dgChecks != null)
                    {
                        dgChecks.ItemsSource = results;
                        UpdateResultsCount(results?.Count ?? 0);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors du filtrage: {ex.Message}",
                    "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void dgChecks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgChecks.SelectedItem != null)
            {
                selectedCheck = dgChecks.SelectedItem as CheckHistory;
                PopulateForm(selectedCheck);
                UpdateButtonVisibility();
            }
        }

        private void dgChecks_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (selectedCheck != null && selectedCheck.CheckImage != null)
            {
                ViewCheckImageFromBytes(selectedCheck.CheckImage);
            }
        }

        private void ViewImage_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            var check = ((FrameworkElement)sender).DataContext as CheckHistory;

            if (check != null && check.CheckImage != null)
            {
                ViewCheckImageFromBytes(check.CheckImage);
            }
            else
            {
                MessageBox.Show("Aucune image disponible pour ce chèque.",
                    "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ViewCheckImageFromBytes(byte[] imageBytes)
        {
            try
            {
                // Create a temporary file to display the image
                string tempPath = Path.Combine(Path.GetTempPath(), $"check_{Guid.NewGuid()}.jpg");
                File.WriteAllBytes(tempPath, imageBytes);

                Process.Start(new ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de l'ouverture de l'image: {ex.Message}",
                    "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PopulateForm(CheckHistory check)
        {
            if (check == null) return;

            txtCheckReference.Text = check.CheckReference;
            txtImagePath.Text = check.CheckImagePath ?? "Image enregistrée dans la base de données";
            selectedImageBytes = check.CheckImage; // Load existing image bytes
            cmbInvoice.SelectedValue = check.InvoiceID;
            txtCheckAmount.Text = check.CheckAmount?.ToString("F2") ?? "";
            dpCheckDate.SelectedDate = check.CheckDate;
            txtBankName.Text = check.BankName;
            txtNotes.Text = check.Notes;

            // Set status
            foreach (ComboBoxItem item in cmbStatus.Items)
            {
                if (item.Content.ToString() == check.CheckStatus)
                {
                    cmbStatus.SelectedItem = item;
                    break;
                }
            }
        }

        private void ClearForm()
        {
            selectedCheck = null;
            selectedImageBytes = null; // Clear image bytes
            txtCheckReference.Clear();
            txtImagePath.Clear();
            cmbInvoice.SelectedIndex = -1;
            txtCheckAmount.Clear();
            dpCheckDate.SelectedDate = DateTime.Now;
            txtBankName.Clear();
            txtNotes.Clear();
            cmbStatus.SelectedIndex = 0;
            dgChecks.SelectedItem = null;
            UpdateButtonVisibility();
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(txtCheckReference.Text))
            {
                MessageBox.Show("Veuillez entrer la référence du chèque.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCheckReference.Focus();
                return false;
            }

            // Only require image for new checks
            if (selectedCheck == null && selectedImageBytes == null)
            {
                MessageBox.Show("Veuillez sélectionner une image du chèque.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (cmbInvoice.SelectedValue == null)
            {
                MessageBox.Show("Veuillez sélectionner une facture.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                cmbInvoice.Focus();
                return false;
            }

            if (!string.IsNullOrEmpty(txtCheckAmount.Text))
            {
                if (!decimal.TryParse(txtCheckAmount.Text, out decimal amount) || amount < 0)
                {
                    MessageBox.Show("Veuillez entrer un montant valide.",
                        "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtCheckAmount.Focus();
                    return false;
                }
            }

            if (dpCheckDate.SelectedDate == null)
            {
                MessageBox.Show("Veuillez sélectionner une date.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                dpCheckDate.Focus();
                return false;
            }

            return true;
        }
    }
}