// JOUST - JC : Outil de Suivi des Tâches - WinForms (.NET Framework 4.x, inclus dans Windows 10/11)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;

[assembly: System.Reflection.AssemblyTitle("JOUST - JC : Outil de Suivi des Tâches")]
[assembly: System.Reflection.AssemblyProduct("JOUST")]
[assembly: System.Reflection.AssemblyVersion("1.6.5.0")]

namespace GestionTaches
{
    public enum Statut { AFaire, Urgent, EnAttente, Fait, EnCours }

    public static class StatutInfo
    {
        public static readonly Statut[] Tous = { Statut.AFaire, Statut.EnCours, Statut.Urgent, Statut.EnAttente, Statut.Fait };

        public static string Libelle(Statut s)
        {
            switch (s)
            {
                case Statut.AFaire: return "À faire";
                case Statut.EnCours: return "En cours";
                case Statut.Urgent: return "Urgent";
                case Statut.EnAttente: return "En attente";
                case Statut.Fait: return "Fait";
            }
            return s.ToString();
        }

        public static Color Couleur(Statut s)
        {
            switch (s)
            {
                case Statut.AFaire: return Color.FromArgb(221, 235, 255);
                case Statut.EnCours: return Color.FromArgb(232, 222, 255);
                case Statut.Urgent: return Color.FromArgb(255, 214, 214);
                case Statut.EnAttente: return Color.FromArgb(255, 240, 200);
                case Statut.Fait: return Color.FromArgb(214, 240, 214);
            }
            return Color.White;
        }
    }

    public class Tache
    {
        public Guid Id { get; set; }
        public string Titre { get; set; }
        public string Projet { get; set; }
        public string Description { get; set; }
        public bool AEcheance { get; set; }
        public DateTime Echeance { get; set; }
        public Statut Statut { get; set; }
        public string Service { get; set; }
        public DateTime Creation { get; set; }
        public DateTime? Terminee { get; set; }

        public Tache()
        {
            Id = Guid.NewGuid();
            Titre = "";
            Projet = "";
            Description = "";
            Service = "";
            Statut = Statut.AFaire;
            Creation = DateTime.Now;
            Echeance = DateTime.Today;
        }

        public bool EnRetard
        {
            get { return AEcheance && Statut != Statut.Fait && Echeance.Date < DateTime.Today; }
        }

        public bool PourAujourdhui
        {
            get { return AEcheance && Statut != Statut.Fait && Echeance.Date == DateTime.Today; }
        }
    }

    public class Donnees
    {
        public List<Tache> Taches { get; set; }
        public List<string> Services { get; set; }
        public List<string> Projets { get; set; }
        public List<Procedure> Procedures { get; set; }
        public Donnees() { Taches = new List<Tache>(); Services = new List<string>(); Projets = new List<string>(); Procedures = new List<Procedure>(); }
    }

    public static class Stockage
    {
        public static string Chemin;
        public const string SansProjet = "Sans projet";

        static Stockage()
        {
            // Mode portable : fichier à côté de l'exe si le dossier est accessible en écriture,
            // sinon dans %APPDATA%\GestionTaches
            string dossierExe = AppDomain.CurrentDomain.BaseDirectory;
            string candidat = Path.Combine(dossierExe, "taches.xml");
            if (PeutEcrire(dossierExe)) { Chemin = candidat; return; }
            string app = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GestionTaches");
            Directory.CreateDirectory(app);
            Chemin = Path.Combine(app, "taches.xml");
        }

        static bool PeutEcrire(string dossier)
        {
            try
            {
                string test = Path.Combine(dossier, ".test_ecriture");
                File.WriteAllText(test, "x");
                File.Delete(test);
                return true;
            }
            catch { return false; }
        }

        public static Donnees Charger()
        {
            try
            {
                if (!File.Exists(Chemin)) return new Donnees();
                Donnees d;
                using (var fs = File.OpenRead(Chemin))
                    d = (Donnees)new XmlSerializer(typeof(Donnees)).Deserialize(fs);
                if (d.Projets == null) d.Projets = new List<string>();
                if (d.Services == null) d.Services = new List<string>();
                if (d.Procedures == null) d.Procedures = new List<Procedure>();
                foreach (var p in d.Procedures) { if (p.Taches == null) p.Taches = new List<LienTache>(); if (p.Projet == null) p.Projet = ""; }
                // Reprise des tâches créées avant l'ajout des projets
                foreach (var t in d.Taches)
                    if (string.IsNullOrWhiteSpace(t.Projet)) t.Projet = Stockage.SansProjet;
                return d;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Impossible de lire le fichier de données :\n" + ex.Message +
                    "\n\nUne copie de sauvegarde sera conservée.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                try { File.Copy(Chemin, Chemin + ".corrompu_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"), true); } catch { }
                return new Donnees();
            }
        }

        public static void Enregistrer(Donnees d)
        {
            string tmp = Chemin + ".tmp";
            using (var fs = File.Create(tmp))
                new XmlSerializer(typeof(Donnees)).Serialize(fs, d);
            if (File.Exists(Chemin))
            {
                File.Copy(Chemin, Chemin + ".bak", true);
                File.Delete(Chemin);
            }
            File.Move(tmp, Chemin);
        }
    }

    // ---------------------------------------------------------------- Fenêtre d'édition
    public class FormTache : Form
    {
        TextBox txtTitre = new TextBox();
        TextBox txtDesc = new TextBox();
        CheckBox chkEcheance = new CheckBox();
        DateTimePicker dtEcheance = new DateTimePicker();
        ComboBox cboStatut = new ComboBox();
        ComboBox cboService = new ComboBox();
        ComboBox cboProjet = new ComboBox();
        Label lblService = new Label();
        public Tache Tache;

        public FormTache(Tache t, IEnumerable<string> services, IEnumerable<string> projets)
        {
            Tache = t;
            Text = string.IsNullOrEmpty(t.Titre) ? "Nouvelle tâche" : "Modifier la tâche";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(460, 440);
            Font = new Font("Segoe UI", 9.5f);

            int x = 16, w = 428, y = 14;
            Controls.Add(new Label { Text = "Titre *", Location = new Point(x, y), AutoSize = true });
            y += 22;
            txtTitre.SetBounds(x, y, w, 26); Controls.Add(txtTitre);
            y += 38;
            Controls.Add(new Label { Text = "Projet *", Location = new Point(x, y + 4), AutoSize = true });
            cboProjet.DropDownStyle = ComboBoxStyle.DropDown;
            cboProjet.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cboProjet.AutoCompleteSource = AutoCompleteSource.ListItems;
            foreach (var p in projets) cboProjet.Items.Add(p);
            cboProjet.SetBounds(x + 110, y, w - 110, 26); Controls.Add(cboProjet);
            y += 38;
            Controls.Add(new Label { Text = "Description / notes", Location = new Point(x, y), AutoSize = true });
            y += 22;
            txtDesc.Multiline = true; txtDesc.ScrollBars = ScrollBars.Vertical;
            txtDesc.SetBounds(x, y, w, 90); Controls.Add(txtDesc);
            y += 102;

            chkEcheance.Text = "Échéance :"; chkEcheance.AutoSize = true;
            chkEcheance.Location = new Point(x, y + 3); Controls.Add(chkEcheance);
            dtEcheance.Format = DateTimePickerFormat.Long;
            dtEcheance.SetBounds(x + 110, y, 250, 26); Controls.Add(dtEcheance);
            chkEcheance.CheckedChanged += delegate { dtEcheance.Enabled = chkEcheance.Checked; };
            y += 40;

            Controls.Add(new Label { Text = "Étiquette", Location = new Point(x, y + 4), AutoSize = true });
            cboStatut.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (var s in StatutInfo.Tous) cboStatut.Items.Add(StatutInfo.Libelle(s));
            cboStatut.SetBounds(x + 110, y, 200, 26); Controls.Add(cboStatut);
            y += 38;

            lblService.Text = "En attente de\n(service)"; lblService.Location = new Point(x, y); lblService.AutoSize = true;
            Controls.Add(lblService);
            cboService.DropDownStyle = ComboBoxStyle.DropDown;
            cboService.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cboService.AutoCompleteSource = AutoCompleteSource.ListItems;
            foreach (var s in services) cboService.Items.Add(s);
            cboService.SetBounds(x + 110, y + 2, 318, 26); Controls.Add(cboService);
            cboStatut.SelectedIndexChanged += delegate { MajService(); };

            var ok = new Button { Text = "Enregistrer", DialogResult = DialogResult.None };
            ok.SetBounds(ClientSize.Width - 222, ClientSize.Height - 46, 100, 32);
            ok.Click += Valider;
            var annuler = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel };
            annuler.SetBounds(ClientSize.Width - 116, ClientSize.Height - 46, 100, 32);
            Controls.Add(ok); Controls.Add(annuler);
            AcceptButton = ok; CancelButton = annuler;

            // Valeurs
            txtTitre.Text = t.Titre;
            cboProjet.Text = t.Projet ?? "";
            txtDesc.Text = t.Description;
            chkEcheance.Checked = t.AEcheance;
            dtEcheance.Value = t.AEcheance ? t.Echeance : DateTime.Today.AddDays(7);
            dtEcheance.Enabled = t.AEcheance;
            cboStatut.SelectedIndex = Array.IndexOf(StatutInfo.Tous, t.Statut);
            cboService.Text = t.Service ?? "";
            MajService();
        }

        void MajService()
        {
            bool attente = cboStatut.SelectedIndex == Array.IndexOf(StatutInfo.Tous, Statut.EnAttente);
            cboService.Enabled = attente;
            lblService.Enabled = attente;
        }

        void Valider(object sender, EventArgs e)
        {
            if (txtTitre.Text.Trim().Length == 0)
            {
                MessageBox.Show("Le titre est obligatoire.", "Tâche", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtTitre.Focus(); return;
            }
            if (cboProjet.Text.Trim().Length == 0)
            {
                MessageBox.Show("Le projet est obligatoire : choisissez-en un dans la liste ou tapez le nom d'un nouveau projet.", "Tâche", MessageBoxButtons.OK, MessageBoxIcon.Information);
                cboProjet.Focus(); return;
            }
            var statut = StatutInfo.Tous[cboStatut.SelectedIndex];
            if (statut == Statut.EnAttente && cboService.Text.Trim().Length == 0)
            {
                MessageBox.Show("Indiquez le service dont vous attendez un retour.", "Tâche", MessageBoxButtons.OK, MessageBoxIcon.Information);
                cboService.Focus(); return;
            }
            Tache.Titre = txtTitre.Text.Trim();
            // Réutilise l'orthographe existante si le projet est déjà connu
            string saisi = cboProjet.Text.Trim();
            foreach (string p in cboProjet.Items)
                if (string.Equals(p, saisi, StringComparison.CurrentCultureIgnoreCase)) { saisi = p; break; }
            Tache.Projet = saisi;
            Tache.Description = txtDesc.Text;
            Tache.AEcheance = chkEcheance.Checked;
            Tache.Echeance = dtEcheance.Value.Date;
            if (statut == Statut.Fait && Tache.Statut != Statut.Fait) Tache.Terminee = DateTime.Now;
            if (statut != Statut.Fait) Tache.Terminee = null;
            Tache.Statut = statut;
            Tache.Service = statut == Statut.EnAttente ? cboService.Text.Trim() : "";
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // ---------------------------------------------------------------- Boîte de choix / saisie
    public class DialogueChoix : Form
    {
        ComboBox cbo = new ComboBox();
        public string Valeur { get { return cbo.Text.Trim(); } }

        public DialogueChoix(string titre, string libelle, IEnumerable<string> choix, string valeur)
        {
            Text = titre;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9.5f);
            ClientSize = new Size(380, 150);
            Controls.Add(new Label { Text = libelle, Location = new Point(16, 12), AutoSize = true, MaximumSize = new Size(350, 0) });
            cbo.DropDownStyle = ComboBoxStyle.DropDown;
            cbo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cbo.AutoCompleteSource = AutoCompleteSource.ListItems;
            foreach (var c in choix) cbo.Items.Add(c);
            cbo.Text = valeur ?? "";
            cbo.SetBounds(16, 58, 348, 26);
            Controls.Add(cbo);
            var ok = new Button { Text = "OK" };
            ok.SetBounds(158, 104, 100, 32);
            ok.Click += delegate
            {
                if (Valeur.Length == 0) { cbo.Focus(); return; }
                DialogResult = DialogResult.OK; Close();
            };
            var annuler = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel };
            annuler.SetBounds(264, 104, 100, 32);
            Controls.Add(ok); Controls.Add(annuler);
            AcceptButton = ok; CancelButton = annuler;
        }
    }

    // Ligne de la liste des projets (panneau de gauche)
    public class ItemProjet
    {
        public string Nom;       // null = tous les projets
        public int Ouvertes, Total, Retard;
        public override string ToString()
        {
            string n = Nom ?? "Tous les projets";
            string r = Retard > 0 ? "  ⚠" + Retard : "";
            return n + "   (" + Ouvertes + "/" + Total + ")" + r;
        }
    }

    // Ligne d'en-tête de groupe dans la grille
    public class EnteteProjet
    {
        public string Nom;
        public int Nb, Ouvertes;
    }

    // ---------------------------------------------------------------- Fenêtre principale
    public class FormPrincipal : Form
    {
        Donnees donnees;
        DataGridView grille = new DataGridView();
        ComboBox cboFiltre = new ComboBox();
        ComboBox cboFiltreService = new ComboBox();
        TextBox txtRecherche = new TextBox();
        CheckBox chkMasquerFaites = new CheckBox();
        CheckBox chkGrouper = new CheckBox();
        ListBox lstProjets = new ListBox();
        ContextMenuStrip menuProjets = new ContextMenuStrip();
        bool majListeEnCours;
        StatusStrip barre = new StatusStrip();
        ToolStripStatusLabel lblStats = new ToolStripStatusLabel();
        ToolStripStatusLabel lblFichier = new ToolStripStatusLabel();
        ContextMenuStrip menuCtx = new ContextMenuStrip();
        TabControl onglets = new TabControl();
        TabPage ongletTaches, ongletProcs;
        PanneauProcedures procs;
        ToolStripMenuItem menuProcsLiees;

        public FormPrincipal()
        {
            Text = "";   // pas de nom dans la barre de titre ni dans la barre des tâches
            Font = new Font("Segoe UI", 9.5f);
            ClientSize = new Size(1366, 720);
            MinimumSize = new Size(1000, 500);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            donnees = Stockage.Charger();

            // Barre d'outils
            var outils = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8, 8, 8, 4), WrapContents = false };
            outils.Controls.Add(Bouton("Nouvelle", 90, delegate { Nouvelle(); }));
            outils.Controls.Add(Bouton("Modifier", 95, delegate { Modifier(); }));
            outils.Controls.Add(Bouton("Supprimer", 105, delegate { Supprimer(); }));
            outils.Controls.Add(Bouton("En cours", 80, delegate { ChangerStatut(Statut.EnCours); }));
            outils.Controls.Add(Bouton("Marquer fait", 100, delegate { ChangerStatut(Statut.Fait); }));
            outils.Controls.Add(Bouton("Export CSV", 95, delegate { ExporterCsv(); }));
            outils.Controls.Add(new Label { Text = "  Étiquette :", AutoSize = true, Margin = new Padding(8, 7, 0, 0) });
            cboFiltre.DropDownStyle = ComboBoxStyle.DropDownList; cboFiltre.Width = 110;
            cboFiltre.Items.Add("Toutes");
            foreach (var s in StatutInfo.Tous) cboFiltre.Items.Add(StatutInfo.Libelle(s));
            cboFiltre.Items.Add("En retard");
            cboFiltre.SelectedIndex = 0;
            cboFiltre.SelectedIndexChanged += delegate { Rafraichir(); };
            cboFiltre.Margin = new Padding(3, 4, 3, 0);
            outils.Controls.Add(cboFiltre);
            outils.Controls.Add(new Label { Text = "Service :", AutoSize = true, Margin = new Padding(8, 7, 0, 0) });
            cboFiltreService.DropDownStyle = ComboBoxStyle.DropDownList; cboFiltreService.Width = 115;
            cboFiltreService.Margin = new Padding(3, 4, 3, 0);
            cboFiltreService.SelectedIndexChanged += delegate { Rafraichir(); };
            outils.Controls.Add(cboFiltreService);
            outils.Controls.Add(new Label { Text = "Rechercher :", AutoSize = true, Margin = new Padding(8, 7, 0, 0) });
            txtRecherche.Width = 110; txtRecherche.Margin = new Padding(3, 4, 3, 0);
            txtRecherche.TextChanged += delegate { Rafraichir(); };
            outils.Controls.Add(txtRecherche);
            chkMasquerFaites.Text = "Masquer faites"; chkMasquerFaites.AutoSize = true; chkMasquerFaites.Margin = new Padding(8, 6, 0, 0);
            chkMasquerFaites.CheckedChanged += delegate { Rafraichir(); };
            outils.Controls.Add(chkMasquerFaites);
            chkGrouper.Text = "Grouper les tâches par projet"; chkGrouper.Dock = DockStyle.Top; chkGrouper.Height = 30;
            chkGrouper.Checked = true;
            chkGrouper.CheckedChanged += delegate { Rafraichir(); };

            // Panneau des projets (à gauche)
            var panneau = new Panel { Dock = DockStyle.Left, Width = 250, Padding = new Padding(8, 0, 4, 8) };
            var titreProjets = new Label
            {
                Text = "PROJETS", Dock = DockStyle.Top, Height = 32, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 62, 80), Padding = new Padding(6, 0, 0, 0)
            };
            lstProjets.Dock = DockStyle.Fill;
            lstProjets.IntegralHeight = false;
            lstProjets.ItemHeight = 26;
            lstProjets.DrawMode = DrawMode.OwnerDrawFixed;
            lstProjets.DrawItem += DessinerProjet;
            lstProjets.SelectedIndexChanged += delegate { if (!majListeEnCours) Rafraichir(); };
            lstProjets.MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Right) return;
                int i = lstProjets.IndexFromPoint(e.Location);
                if (i >= 0) lstProjets.SelectedIndex = i;
            };
            lstProjets.DoubleClick += delegate { RenommerProjet(); };
            menuProjets.Items.Add("Nouvelle tâche dans ce projet…", null, delegate { Nouvelle(); });
            menuProjets.Items.Add("Renommer le projet…", null, delegate { RenommerProjet(); });
            menuProjets.Items.Add("Voir les procédures du projet", null, delegate
            {
                onglets.SelectedTab = ongletProcs;
                procs.SelectionnerProjet(ProjetSelectionne());
            });
            menuProjets.Items.Add("Retirer de la liste (projet vide)", null, delegate { RetirerProjet(); });
            lstProjets.ContextMenuStrip = menuProjets;
            var aide = new Label
            {
                Text = "Clic : filtrer • Double-clic : renommer\nouvertes / total — rouge : en retard",
                Dock = DockStyle.Bottom, Height = 42, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8.25f)
            };
            panneau.Controls.Add(lstProjets);
            panneau.Controls.Add(aide);
            panneau.Controls.Add(chkGrouper);
            panneau.Controls.Add(titreProjets);

            // Grille
            grille.Dock = DockStyle.Fill;
            grille.ReadOnly = true;
            grille.AllowUserToAddRows = false;
            grille.AllowUserToDeleteRows = false;
            grille.AllowUserToResizeRows = false;
            grille.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grille.MultiSelect = true;
            grille.RowHeadersVisible = false;
            grille.BackgroundColor = Color.White;
            grille.BorderStyle = BorderStyle.None;
            grille.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grille.ColumnHeadersHeight = 32;
            grille.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grille.RowTemplate.Height = 28;
            grille.EnableHeadersVisualStyles = false;
            grille.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 62, 80);
            grille.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grille.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            grille.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 215);
            AjouterColonne("Echeance", "Échéance", 12);
            AjouterColonne("Titre", "Tâche", 34);
            AjouterColonne("Projet", "Projet", 14);
            AjouterColonne("Statut", "Étiquette", 12);
            AjouterColonne("Service", "En attente de", 16);
            AjouterColonne("Jours", "Reste", 9);
            AjouterColonne("Creation", "Créée le", 11);
            AjouterColonne("Procs", "Procédures", 9);
            foreach (DataGridViewColumn c in grille.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
            grille.CellDoubleClick += delegate(object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) Modifier(); };
            grille.RowPrePaint += DessinerEntete;
            grille.CellMouseDown += delegate(object s, DataGridViewCellMouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && !grille.Rows[e.RowIndex].Selected)
                {
                    grille.ClearSelection();
                    grille.Rows[e.RowIndex].Selected = true;
                }
            };

            // Menu contextuel
            menuCtx.Items.Add("Modifier…", null, delegate { Modifier(); });
            menuCtx.Items.Add(new ToolStripSeparator());
            foreach (var s in StatutInfo.Tous)
            {
                var st = s;
                menuCtx.Items.Add("Étiqueter : " + StatutInfo.Libelle(st), null, delegate { ChangerStatut(st); });
            }
            menuCtx.Items.Add(new ToolStripSeparator());
            menuCtx.Items.Add("Changer de projet…", null, delegate { ChangerProjet(); });
            menuCtx.Items.Add(new ToolStripSeparator());
            menuProcsLiees = new ToolStripMenuItem("Procédures liées");
            menuCtx.Items.Add(menuProcsLiees);
            menuCtx.Items.Add("Nouvelle procédure pour cette/ces tâche(s)…", null, delegate
            {
                var sel = SelectionTaches();
                if (sel.Count == 0) return;
                onglets.SelectedTab = ongletProcs;
                procs.Nouvelle(sel[0].Projet, sel);
            });
            menuCtx.Opening += delegate
            {
                menuProcsLiees.DropDownItems.Clear();
                var sel = SelectionTaches();
                var liees = sel.Count == 0 ? new List<Procedure>() :
                    donnees.Procedures.Where(pr => pr.ConcerneTache(sel[0].Id)).OrderBy(pr => pr.Titre).ToList();
                menuProcsLiees.Text = "Procédures liées (" + liees.Count + ")";
                menuProcsLiees.Enabled = liees.Count > 0;
                foreach (var pr in liees)
                {
                    var proc = pr;
                    menuProcsLiees.DropDownItems.Add(proc.Titre, null, delegate { procs.Ouvrir(proc); Rafraichir(); });
                }
                if (liees.Count > 0)
                {
                    menuProcsLiees.DropDownItems.Add(new ToolStripSeparator());
                    var t0 = sel[0];
                    menuProcsLiees.DropDownItems.Add("Afficher dans l'onglet Procédures", null, delegate
                    {
                        onglets.SelectedTab = ongletProcs;
                        procs.FiltrerSurTache(t0);
                    });
                }
            };
            menuCtx.Items.Add(new ToolStripSeparator());
            menuCtx.Items.Add("Supprimer", null, delegate { Supprimer(); });
            grille.ContextMenuStrip = menuCtx;

            // Barre d'état
            lblStats.Spring = true; lblStats.TextAlign = ContentAlignment.MiddleLeft;
            lblFichier.Text = "Données : " + Stockage.Chemin;
            lblFichier.ForeColor = Color.Gray;
            barre.Items.Add(lblStats); barre.Items.Add(lblFichier);

            // Légende
            var legende = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(8, 4, 8, 4) };
            foreach (var s in StatutInfo.Tous)
                legende.Controls.Add(new Label { Text = " " + StatutInfo.Libelle(s) + " ", AutoSize = true, BackColor = StatutInfo.Couleur(s), Margin = new Padding(0, 0, 8, 0), Padding = new Padding(4, 2, 4, 2) });
            legende.Controls.Add(new Label { Text = " Échéance dépassée ", AutoSize = true, ForeColor = Color.DarkRed, Font = new Font(Font, FontStyle.Bold), Padding = new Padding(4, 2, 4, 2) });

            ongletTaches = new TabPage("   Tâches   ");
            ongletTaches.Controls.Add(grille);
            ongletTaches.Controls.Add(panneau);
            ongletTaches.Controls.Add(legende);
            ongletTaches.Controls.Add(outils);

            procs = new PanneauProcedures(donnees) { Dock = DockStyle.Fill };
            procs.Sauver = Sauver;
            procs.ProjetsConnus = ProjetsConnus;
            procs.ApresModification = delegate { MajListeProjets(); Rafraichir(); };
            ongletProcs = new TabPage("   Procédures   ");
            ongletProcs.Controls.Add(procs);

            onglets.Dock = DockStyle.Fill;
            onglets.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            onglets.Padding = new Point(14, 5);
            onglets.TabPages.Add(ongletTaches);
            onglets.TabPages.Add(ongletProcs);
            foreach (TabPage tp in onglets.TabPages) { tp.Font = Font; tp.UseVisualStyleBackColor = true; }
            onglets.SelectedIndexChanged += delegate
            {
                if (onglets.SelectedTab == ongletProcs) { procs.MajProjets(); procs.Rafraichir(); }
                else Rafraichir();
            };

            Controls.Add(onglets);

            // En-tête : petit « J » ; un clic affiche le logo complet dans l'en-tête (fond sombre), un 2e clic le replie
            bandeau = new Panel { Dock = DockStyle.Top, Height = 40 };
            enteteCompact = new Panel { Dock = DockStyle.Fill };
            try
            {
                var flux = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("logo.png");
                if (flux != null)
                {
                    var pic = new PictureBox { Image = Image.FromStream(flux), SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(10, 5, 30, 30), Cursor = Cursors.Hand };
                    pic.Click += delegate { BasculerLogo(); };
                    new ToolTip().SetToolTip(pic, "Afficher le logo");
                    enteteCompact.Controls.Add(pic);
                }
                var flux2 = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("logo_complet.png");
                if (flux2 != null)
                {
                    logoComplet = new PictureBox { Image = Image.FromStream(flux2), SizeMode = PictureBoxSizeMode.Zoom, Cursor = Cursors.Hand, Visible = false };
                    logoComplet.Click += delegate { BasculerLogo(); };
                    new ToolTip().SetToolTip(logoComplet, "Cliquer pour replier");
                    bandeau.Controls.Add(logoComplet);
                }
            }
            catch { }
            bandeau.Controls.Add(enteteCompact);
            Controls.Add(bandeau);
            Controls.Add(barre);

            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.N)
                {
                    if (onglets.SelectedTab == ongletProcs) procs.Nouvelle(null, null); else Nouvelle();
                    e.Handled = true;
                }
                else if (onglets.SelectedTab == ongletProcs) return;
                else if (e.KeyCode == Keys.Delete && grille.Focused) { Supprimer(); e.Handled = true; }
                else if (e.KeyCode == Keys.Enter && grille.Focused) { Modifier(); e.Handled = true; }
                else if (e.Control && e.KeyCode == Keys.F) { txtRecherche.Focus(); e.Handled = true; }
            };

            MajServicesFiltre();
            MajListeProjets();
            procs.MajProjets();
            Rafraichir();
            Shown += delegate { try { lstProjets.TopIndex = 0; } catch { } AlerteDemarrage(); };
        }

        Panel bandeau, enteteCompact;
        PictureBox logoComplet;

        void BasculerLogo()
        {
            if (logoComplet == null) return;
            bool afficher = !logoComplet.Visible;
            bandeau.SuspendLayout();
            if (afficher)
            {
                var img = logoComplet.Image;
                int h = 120;
                bandeau.Height = h;
                bandeau.BackColor = Color.FromArgb(24, 32, 46);      // même fond que l'image du logo
                logoComplet.SetBounds(0, 0, img.Width * h / img.Height, h);
            }
            else
            {
                bandeau.Height = 40;
                bandeau.BackColor = SystemColors.Control;
            }
            enteteCompact.Visible = !afficher;
            logoComplet.Visible = afficher;
            bandeau.ResumeLayout();
        }

        Button Bouton(string texte, int largeur, EventHandler clic)
        {
            var b = new Button { Text = texte, Width = largeur, Height = 30, Margin = new Padding(0, 0, 6, 0) };
            b.Click += clic;
            return b;
        }

        void AjouterColonne(string nom, string entete, int poids)
        {
            var c = new DataGridViewTextBoxColumn { Name = nom, HeaderText = entete, FillWeight = poids };
            grille.Columns.Add(c);
        }

        void MajServicesFiltre()
        {
            string courant = cboFiltreService.SelectedItem as string;
            cboFiltreService.Items.Clear();
            cboFiltreService.Items.Add("Tous");
            foreach (var s in ServicesConnus()) cboFiltreService.Items.Add(s);
            int i = courant == null ? 0 : cboFiltreService.Items.IndexOf(courant);
            cboFiltreService.SelectedIndex = i < 0 ? 0 : i;
        }

        List<string> ServicesConnus()
        {
            return donnees.Services
                .Concat(donnees.Taches.Where(t => !string.IsNullOrEmpty(t.Service)).Select(t => t.Service))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        int NbProcs(Tache t) { return donnees.Procedures.Count(p => p.ConcerneTache(t.Id)); }

        List<string> ProjetsConnus()
        {
            return donnees.Projets
                .Concat(donnees.Taches.Where(t => !string.IsNullOrEmpty(t.Projet)).Select(t => t.Projet))
                .Concat(donnees.Procedures.Where(p => !string.IsNullOrEmpty(p.Projet)).Select(p => p.Projet))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        string ProjetSelectionne()
        {
            var it = lstProjets.SelectedItem as ItemProjet;
            return it == null ? null : it.Nom;
        }

        void MajListeProjets()
        {
            string courant = ProjetSelectionne();
            majListeEnCours = true;
            lstProjets.SelectedIndex = -1;
            lstProjets.BeginUpdate();
            lstProjets.Items.Clear();
            lstProjets.Items.Add(new ItemProjet
            {
                Nom = null,
                Total = donnees.Taches.Count,
                Ouvertes = donnees.Taches.Count(t => t.Statut != Statut.Fait),
                Retard = donnees.Taches.Count(t => t.EnRetard)
            });
            int aSelectionner = 0;
            foreach (var p in ProjetsConnus())
            {
                var tp = donnees.Taches.Where(t => string.Equals(t.Projet, p, StringComparison.CurrentCultureIgnoreCase)).ToList();
                int i = lstProjets.Items.Add(new ItemProjet
                {
                    Nom = p, Total = tp.Count,
                    Ouvertes = tp.Count(t => t.Statut != Statut.Fait),
                    Retard = tp.Count(t => t.EnRetard)
                });
                if (courant != null && string.Equals(courant, p, StringComparison.CurrentCultureIgnoreCase)) aSelectionner = i;
            }
            lstProjets.SelectedIndex = aSelectionner;
            lstProjets.EndUpdate();
            if (aSelectionner == 0) lstProjets.TopIndex = 0;
            majListeEnCours = false;
        }

        void DessinerProjet(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var it = (ItemProjet)lstProjets.Items[e.Index];
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var fond = new SolidBrush(sel ? Color.FromArgb(0, 120, 215) : (e.Index == 0 ? Color.FromArgb(240, 242, 245) : Color.White)))
                e.Graphics.FillRectangle(fond, e.Bounds);
            var police = e.Index == 0 ? new Font(Font, FontStyle.Bold) : Font;
            var r = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 70, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, it.Nom ?? "Tous les projets", police, r, sel ? Color.White : Color.Black,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.Left);
            string compte = it.Ouvertes + "/" + it.Total;
            var rc = new Rectangle(e.Bounds.Right - 64, e.Bounds.Y, 58, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, compte, Font, rc,
                sel ? Color.White : (it.Retard > 0 ? Color.DarkRed : Color.Gray),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            if (e.Index == 0) police.Dispose();
        }

        void DessinerEntete(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            var en = grille.Rows[e.RowIndex].Tag as EnteteProjet;
            if (en == null) return;
            var b = e.RowBounds;
            using (var fond = new SolidBrush(Color.FromArgb(232, 236, 241)))
                e.Graphics.FillRectangle(fond, b);
            using (var trait = new Pen(Color.FromArgb(45, 62, 80), 2))
                e.Graphics.DrawLine(trait, b.Left, b.Bottom - 1, b.Right, b.Bottom - 1);
            using (var gras = new Font("Segoe UI", 10f, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, en.Nom, gras, new Rectangle(b.X + 8, b.Y, b.Width - 16, b.Height),
                    Color.FromArgb(45, 62, 80), TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            TextRenderer.DrawText(e.Graphics, en.Ouvertes + " ouverte(s) / " + en.Nb + " tâche(s)", Font,
                new Rectangle(b.X + 8, b.Y, b.Width - 16, b.Height), Color.DimGray, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            e.Handled = true;
        }

        IEnumerable<Tache> TachesFiltrees()
        {
            IEnumerable<Tache> q = donnees.Taches;
            string projet = ProjetSelectionne();
            if (projet != null) q = q.Where(t => string.Equals(t.Projet, projet, StringComparison.CurrentCultureIgnoreCase));
            int f = cboFiltre.SelectedIndex;
            if (f >= 1 && f <= StatutInfo.Tous.Length) { var st = StatutInfo.Tous[f - 1]; q = q.Where(t => t.Statut == st); }
            else if (f == StatutInfo.Tous.Length + 1) q = q.Where(t => t.EnRetard);
            if (cboFiltreService.SelectedIndex > 0)
            {
                string svc = (string)cboFiltreService.SelectedItem;
                q = q.Where(t => string.Equals(t.Service, svc, StringComparison.CurrentCultureIgnoreCase));
            }
            if (chkMasquerFaites.Checked) q = q.Where(t => t.Statut != Statut.Fait);
            string r = txtRecherche.Text.Trim();
            if (r.Length > 0)
                q = q.Where(t => (t.Titre + " " + t.Description + " " + t.Service + " " + t.Projet).IndexOf(r, StringComparison.CurrentCultureIgnoreCase) >= 0);

            // Tri : non faites d'abord, puis urgentes, puis par échéance (sans échéance à la fin)
            return q.OrderBy(t => t.Statut == Statut.Fait ? 1 : 0)
                    .ThenBy(t => t.Statut == Statut.Urgent ? 0 : 1)
                    .ThenBy(t => t.AEcheance ? 0 : 1)
                    .ThenBy(t => t.Echeance)
                    .ThenBy(t => t.Creation);
        }

        void Rafraichir()
        {
            var selection = new HashSet<Guid>(SelectionTaches().Select(t => t.Id));
            grille.Rows.Clear();
            var liste = TachesFiltrees().ToList();
            bool grouper = chkGrouper.Checked && ProjetSelectionne() == null;
            grille.Columns["Projet"].Visible = !grouper;
            if (grouper)
                liste = liste.OrderBy(t => t.Projet == Stockage.SansProjet ? 1 : 0)
                             .ThenBy(t => t.Projet, StringComparer.CurrentCultureIgnoreCase).ToList(); // tri stable : l'ordre interne est conservé
            string groupeCourant = null;
            int nbTaches = 0;
            foreach (var t in liste)
            {
                nbTaches++;
                if (grouper && !string.Equals(groupeCourant, t.Projet, StringComparison.CurrentCultureIgnoreCase))
                {
                    groupeCourant = t.Projet;
                    var g = liste.Where(x => string.Equals(x.Projet, groupeCourant, StringComparison.CurrentCultureIgnoreCase)).ToList();
                    int ih = grille.Rows.Add();
                    grille.Rows[ih].Tag = new EnteteProjet { Nom = groupeCourant, Nb = g.Count, Ouvertes = g.Count(x => x.Statut != Statut.Fait) };
                    grille.Rows[ih].Height = 32;
                }
                string reste = "";
                if (t.AEcheance && t.Statut != Statut.Fait)
                {
                    int j = (t.Echeance.Date - DateTime.Today).Days;
                    reste = j == 0 ? "Aujourd'hui" : j < 0 ? (-j) + " j de retard" : j + " j";
                }
                int i = grille.Rows.Add(
                    t.AEcheance ? t.Echeance.ToString("dd/MM/yyyy") : "—",
                    t.Titre,
                    t.Projet,
                    StatutInfo.Libelle(t.Statut),
                    t.Statut == Statut.EnAttente ? t.Service : "",
                    reste,
                    t.Creation.ToString("dd/MM/yyyy"),
                    NbProcs(t) == 0 ? "" : NbProcs(t).ToString());
                var row = grille.Rows[i];
                row.Tag = t;
                row.DefaultCellStyle.BackColor = StatutInfo.Couleur(t.Statut);
                if (!string.IsNullOrEmpty(t.Description))
                    row.Cells["Titre"].ToolTipText = t.Description;
                if (t.Statut == Statut.Fait)
                    row.DefaultCellStyle.ForeColor = Color.Gray;
                if (t.EnRetard)
                {
                    row.Cells["Echeance"].Style.ForeColor = Color.DarkRed;
                    row.Cells["Jours"].Style.ForeColor = Color.DarkRed;
                    row.Cells["Echeance"].Style.Font = new Font(Font, FontStyle.Bold);
                    row.Cells["Jours"].Style.Font = new Font(Font, FontStyle.Bold);
                }
                else if (t.PourAujourdhui)
                {
                    row.Cells["Jours"].Style.Font = new Font(Font, FontStyle.Bold);
                }
                row.Selected = selection.Contains(t.Id);
            }
            if (selection.Count == 0) grille.ClearSelection();

            int total = donnees.Taches.Count;
            Func<Statut, int> n = s => donnees.Taches.Count(t => t.Statut == s);
            int retard = donnees.Taches.Count(t => t.EnRetard);
            lblStats.Text = string.Format("{0} affichée(s) / {1}  |  À faire : {2}  |  En cours : {7}  |  Urgent : {3}  |  En attente : {4}  |  Fait : {5}  |  En retard : {6}",
                nbTaches, total, n(Statut.AFaire), n(Statut.Urgent), n(Statut.EnAttente), n(Statut.Fait), retard, n(Statut.EnCours));
        }


        List<Tache> SelectionTaches()
        {
            var l = new List<Tache>();
            foreach (DataGridViewRow r in grille.SelectedRows)
                if (r.Tag is Tache) l.Add((Tache)r.Tag);
            return l;
        }

        void Sauver()
        {
            try { Stockage.Enregistrer(donnees); }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur d'enregistrement :\n" + ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void MemoriserProjet(Tache t)
        {
            if (!string.IsNullOrEmpty(t.Projet) &&
                !donnees.Projets.Any(s => string.Equals(s, t.Projet, StringComparison.CurrentCultureIgnoreCase)))
                donnees.Projets.Add(t.Projet);
        }

        void ChangerProjet()
        {
            var sel = SelectionTaches();
            if (sel.Count == 0) return;
            using (var d = new DialogueChoix("Changer de projet", "Projet pour " + (sel.Count == 1 ? "la tâche « " + sel[0].Titre + " »" : "les " + sel.Count + " tâches sélectionnées") + " :",
                ProjetsConnus(), sel[0].Projet))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string nom = ProjetsConnus().FirstOrDefault(p => string.Equals(p, d.Valeur, StringComparison.CurrentCultureIgnoreCase)) ?? d.Valeur;
                foreach (var t in sel) { t.Projet = nom; MemoriserProjet(t); }
            }
            Sauver(); MajListeProjets(); Rafraichir();
        }

        void RenommerProjet()
        {
            string ancien = ProjetSelectionne();
            if (ancien == null) return;
            using (var d = new DialogueChoix("Renommer le projet", "Nouveau nom du projet « " + ancien + " » :", new string[0], ancien))
            {
                if (d.ShowDialog(this) != DialogResult.OK || d.Valeur == ancien) return;
                string nouveau = ProjetsConnus().FirstOrDefault(p => string.Equals(p, d.Valeur, StringComparison.CurrentCultureIgnoreCase)
                                                                   && !string.Equals(p, ancien, StringComparison.CurrentCultureIgnoreCase)) ?? d.Valeur;
                if (nouveau != d.Valeur &&
                    MessageBox.Show("Le projet « " + nouveau + " » existe déjà. Fusionner les deux projets ?", "Renommer",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                foreach (var t in donnees.Taches)
                    if (string.Equals(t.Projet, ancien, StringComparison.CurrentCultureIgnoreCase)) t.Projet = nouveau;
                foreach (var pr in donnees.Procedures)
                    if (string.Equals(pr.Projet, ancien, StringComparison.CurrentCultureIgnoreCase)) pr.Projet = nouveau;
                donnees.Projets.RemoveAll(p => string.Equals(p, ancien, StringComparison.CurrentCultureIgnoreCase));
                if (!donnees.Projets.Any(p => string.Equals(p, nouveau, StringComparison.CurrentCultureIgnoreCase))) donnees.Projets.Add(nouveau);
                majListeEnCours = true;
                lstProjets.SelectedIndex = 0;          // puis re-sélection du nouveau nom
                majListeEnCours = false;
                Sauver(); MajListeProjets();
                for (int i = 1; i < lstProjets.Items.Count; i++)
                    if (((ItemProjet)lstProjets.Items[i]).Nom == nouveau) { lstProjets.SelectedIndex = i; break; }
                Rafraichir();
            }
        }

        void RetirerProjet()
        {
            string nom = ProjetSelectionne();
            if (nom == null) return;
            if (donnees.Taches.Any(t => string.Equals(t.Projet, nom, StringComparison.CurrentCultureIgnoreCase)) ||
                donnees.Procedures.Any(t => string.Equals(t.Projet, nom, StringComparison.CurrentCultureIgnoreCase)))
            {
                MessageBox.Show("Ce projet contient encore des tâches ou des procédures. Déplacez-les ou supprimez-les d'abord.", "Projet",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            donnees.Projets.RemoveAll(p => string.Equals(p, nom, StringComparison.CurrentCultureIgnoreCase));
            Sauver(); MajListeProjets(); Rafraichir();
        }

        void MemoriserService(Tache t)
        {
            if (!string.IsNullOrEmpty(t.Service) &&
                !donnees.Services.Any(s => string.Equals(s, t.Service, StringComparison.CurrentCultureIgnoreCase)))
                donnees.Services.Add(t.Service);
        }

        void Nouvelle()
        {
            var t = new Tache();
            t.Projet = ProjetSelectionne() ?? "";
            using (var f = new FormTache(t, ServicesConnus(), ProjetsConnus()))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
            }
            donnees.Taches.Add(t);
            MemoriserService(t);
            MemoriserProjet(t);
            Sauver(); MajServicesFiltre(); MajListeProjets(); Rafraichir();
        }

        void Modifier()
        {
            var sel = SelectionTaches();
            if (sel.Count == 0) return;
            var t = sel[0];
            using (var f = new FormTache(t, ServicesConnus(), ProjetsConnus()))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
            }
            MemoriserService(t);
            MemoriserProjet(t);
            Sauver(); MajServicesFiltre(); MajListeProjets(); Rafraichir();
        }

        void Supprimer()
        {
            var sel = SelectionTaches();
            if (sel.Count == 0) return;
            string msg = sel.Count == 1 ? "Supprimer la tâche « " + sel[0].Titre + " » ?" : "Supprimer les " + sel.Count + " tâches sélectionnées ?";
            if (MessageBox.Show(msg, "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            foreach (var t in sel)
            {
                foreach (var pr in donnees.Procedures)
                    foreach (var l in pr.Taches.Where(l => l.TacheId == t.Id)) l.Libelle = t.Titre;
                donnees.Taches.Remove(t);
            }
            Sauver(); MajServicesFiltre(); MajListeProjets(); Rafraichir();
        }

        void ChangerStatut(Statut s)
        {
            var sel = SelectionTaches();
            if (sel.Count == 0) return;
            if (s == Statut.EnAttente)
            {
                // Demande le service via la fenêtre d'édition
                foreach (var t in sel)
                {
                    var ancien = t.Statut;
                    t.Statut = Statut.EnAttente;
                    using (var f = new FormTache(t, ServicesConnus(), ProjetsConnus()))
                    {
                        if (f.ShowDialog(this) != DialogResult.OK) { t.Statut = ancien; continue; }
                    }
                    MemoriserService(t);
                }
            }
            else
            {
                foreach (var t in sel)
                {
                    if (s == Statut.Fait && t.Statut != Statut.Fait) t.Terminee = DateTime.Now;
                    if (s != Statut.Fait) t.Terminee = null;
                    t.Statut = s;
                    t.Service = "";
                }
            }
            Sauver(); MajServicesFiltre(); MajListeProjets(); Rafraichir();
        }

        void ExporterCsv()
        {
            using (var dlg = new SaveFileDialog { Filter = "Fichier CSV (*.csv)|*.csv", FileName = "taches_" + DateTime.Today.ToString("yyyy-MM-dd") + ".csv" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var sb = new StringBuilder();
                sb.AppendLine("Échéance;Projet;Tâche;Description;Étiquette;En attente de;Créée le;Terminée le");
                foreach (var t in TachesFiltrees())
                {
                    sb.AppendLine(string.Join(";", new[] {
                        t.AEcheance ? t.Echeance.ToString("dd/MM/yyyy") : "",
                        Csv(t.Projet), Csv(t.Titre), Csv(t.Description), StatutInfo.Libelle(t.Statut), Csv(t.Service),
                        t.Creation.ToString("dd/MM/yyyy"),
                        t.Terminee.HasValue ? t.Terminee.Value.ToString("dd/MM/yyyy") : "" }));
                }
                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                MessageBox.Show("Export terminé.", "Export CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        static string Csv(string s)
        {
            s = s ?? "";
            if (s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0) return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        void AlerteDemarrage()
        {
            int retard = donnees.Taches.Count(t => t.EnRetard);
            int jour = donnees.Taches.Count(t => t.PourAujourdhui);
            if (retard + jour == 0) return;
            var msg = new StringBuilder();
            if (retard > 0) msg.AppendLine(retard + " tâche(s) en retard.");
            if (jour > 0) msg.AppendLine(jour + " tâche(s) à rendre aujourd'hui.");
            MessageBox.Show(this, msg.ToString(), "Rappel des échéances", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormPrincipal());
        }
    }
}
