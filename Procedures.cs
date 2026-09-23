// Module Procédures
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace GestionTaches
{
    // Lien vers une tâche : soit une tâche du module Tâches (TacheId), soit un simple libellé libre (TacheId = Guid.Empty)
    public class LienTache
    {
        public Guid TacheId { get; set; }
        public string Libelle { get; set; }
        public LienTache() { Libelle = ""; }
    }

    public class Procedure
    {
        public Guid Id { get; set; }
        public string Titre { get; set; }
        public string Projet { get; set; }          // vide = procédure générale
        public List<LienTache> Taches { get; set; }
        public string Rtf { get; set; }
        public string Texte { get; set; }           // texte brut (recherche)
        public DateTime Creation { get; set; }
        public DateTime Modification { get; set; }

        public Procedure()
        {
            Id = Guid.NewGuid();
            Titre = ""; Projet = ""; Rtf = ""; Texte = "";
            Taches = new List<LienTache>();
            Creation = DateTime.Now; Modification = DateTime.Now;
        }

        public bool ConcerneTache(Guid id) { return Taches.Any(l => l.TacheId == id); }
    }

    public static class OutilsProc
    {
        public const string General = "Général (sans projet)";

        public static string LibelleLien(LienTache l, Donnees d)
        {
            if (l.TacheId != Guid.Empty)
            {
                var t = d.Taches.FirstOrDefault(x => x.Id == l.TacheId);
                if (t != null) return t.Titre + "  [" + t.Projet + " • " + StatutInfo.Libelle(t.Statut) + "]";
            }
            return l.Libelle + (l.TacheId != Guid.Empty ? "  (tâche supprimée)" : "  (hors liste)");
        }

        public static string NomProjet(Procedure p) { return string.IsNullOrEmpty(p.Projet) ? OutilsProc.General : p.Projet; }

        public static void Exporter(IWin32Window owner, Procedure p, Donnees d)
        {
            using (var dlg = new SaveFileDialog
            {
                Filter = "Document RTF (Word, WordPad) (*.rtf)|*.rtf",
                FileName = NettoyerNom(p.Titre) + ".rtf"
            })
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK) return;
                using (var r = new RichTextBox())
                {
                    r.Font = new Font("Segoe UI", 10f);
                    try { r.Rtf = p.Rtf; } catch { r.Text = p.Texte; }
                    string entete = p.Titre + "\n";
                    string meta = "Projet : " + NomProjet(p) + "\n";
                    if (p.Taches.Count > 0)
                        meta += "Tâches concernées : " + string.Join(" ; ", p.Taches.Select(l => LibelleLien(l, d)).ToArray()) + "\n";
                    meta += "Mise à jour : " + p.Modification.ToString("dd/MM/yyyy") + "\n\n";
                    r.Select(0, 0);
                    r.SelectedText = entete + meta;
                    r.Select(0, entete.Length);
                    r.SelectionFont = new Font("Segoe UI", 16f, FontStyle.Bold);
                    r.Select(entete.Length, meta.Length);
                    r.SelectionFont = new Font("Segoe UI", 9f, FontStyle.Italic);
                    r.SelectionColor = Color.DimGray;
                    r.SaveFile(dlg.FileName, RichTextBoxStreamType.RichText);
                }
                MessageBox.Show(owner, "Procédure exportée :\n" + dlg.FileName, "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        static string NettoyerNom(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Length == 0 ? "procedure" : s;
        }
    }

    // ---------------------------------------------------------------- Choix de tâches existantes
    public class FormChoixTaches : Form
    {
        CheckedListBox lst = new CheckedListBox();
        CheckBox chkProjet = new CheckBox();
        TextBox txtFiltre = new TextBox();
        Donnees donnees;
        string projet;
        public HashSet<Guid> Coches;
        bool remplissage;

        class Item
        {
            public Tache T;
            public override string ToString() { return T.Titre + "   [" + T.Projet + " • " + StatutInfo.Libelle(T.Statut) + "]"; }
        }

        public FormChoixTaches(Donnees d, string projetProc, IEnumerable<Guid> dejaLies)
        {
            donnees = d; projet = projetProc;
            Coches = new HashSet<Guid>(dejaLies);
            Text = "Lier des tâches existantes";
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9.5f);
            ClientSize = new Size(560, 480);
            MinimizeBox = false;

            var haut = new Panel { Dock = DockStyle.Top, Height = 64, Width = 560, Padding = new Padding(10, 8, 10, 0) };
            chkProjet.Text = string.IsNullOrEmpty(projet) ? "(la procédure n'a pas de projet : toutes les tâches sont listées)" : "Uniquement les tâches du projet « " + projet + " »";
            chkProjet.Checked = !string.IsNullOrEmpty(projet);
            chkProjet.Enabled = !string.IsNullOrEmpty(projet);
            chkProjet.SetBounds(10, 6, 530, 24);
            chkProjet.CheckedChanged += delegate { Remplir(); };
            haut.Controls.Add(chkProjet);
            haut.Controls.Add(new Label { Text = "Filtrer :", Location = new Point(10, 37), AutoSize = true });
            txtFiltre.SetBounds(70, 34, 470, 24);
            txtFiltre.TextChanged += delegate { Remplir(); };
            haut.Controls.Add(txtFiltre);

            lst.Dock = DockStyle.Fill;
            lst.CheckOnClick = true;
            lst.IntegralHeight = false;
            lst.ItemCheck += delegate(object s, ItemCheckEventArgs e)
            {
                if (remplissage) return;
                var it = (Item)lst.Items[e.Index];
                if (e.NewValue == CheckState.Checked) Coches.Add(it.T.Id); else Coches.Remove(it.T.Id);
            };

            var bas = new Panel { Dock = DockStyle.Bottom, Height = 50, Width = 560 };
            var ok = new Button { Text = "Valider", DialogResult = DialogResult.OK };
            var annuler = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel };
            ok.SetBounds(ClientSize.Width - 222, 9, 100, 32); annuler.SetBounds(ClientSize.Width - 116, 9, 100, 32);
            ok.Anchor = annuler.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bas.Controls.Add(ok); bas.Controls.Add(annuler);
            AcceptButton = ok; CancelButton = annuler;

            Controls.Add(lst); Controls.Add(haut); Controls.Add(bas);
            Remplir();
        }

        void Remplir()
        {
            remplissage = true;
            lst.BeginUpdate();
            lst.Items.Clear();
            IEnumerable<Tache> q = donnees.Taches;
            if (chkProjet.Checked) q = q.Where(t => string.Equals(t.Projet, projet, StringComparison.CurrentCultureIgnoreCase));
            string f = txtFiltre.Text.Trim();
            if (f.Length > 0) q = q.Where(t => (t.Titre + " " + t.Projet).IndexOf(f, StringComparison.CurrentCultureIgnoreCase) >= 0);
            foreach (var t in q.OrderBy(t => t.Projet).ThenBy(t => t.Statut == Statut.Fait ? 1 : 0).ThenBy(t => t.Titre))
            {
                int i = lst.Items.Add(new Item { T = t });
                lst.SetItemChecked(i, Coches.Contains(t.Id));
            }
            lst.EndUpdate();
            remplissage = false;
        }
    }

    // ---------------------------------------------------------------- Éditeur de procédure
    public class FormProcedure : Form
    {
        TextBox txtTitre = new TextBox();
        ComboBox cboProjet = new ComboBox();
        RichTextBox rtb = new RichTextBox();
        ListBox lstLiens = new ListBox();
        List<LienTache> liens;
        Donnees donnees;
        public Procedure Proc;

        const string Modele =
            "Objectif\n\n\n" +
            "Prérequis\n\n\n" +
            "Étapes\n1. \n2. \n3. \n\n" +
            "Points de vigilance\n\n\n" +
            "Contacts / services concernés\n\n";

        public FormProcedure(Procedure p, Donnees d, List<string> projets)
        {
            Proc = p; donnees = d;
            liens = p.Taches.Select(l => new LienTache { TacheId = l.TacheId, Libelle = l.Libelle }).ToList();
            Text = string.IsNullOrEmpty(p.Titre) ? "Nouvelle procédure" : "Procédure : " + p.Titre;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9.5f);
            ClientSize = new Size(1000, 700);
            MinimumSize = new Size(760, 500);
            KeyPreview = true;

            // En-tête : titre + projet
            var haut = new Panel { Dock = DockStyle.Top, Height = 78, Width = 1000, Padding = new Padding(12, 8, 12, 0) };
            haut.Controls.Add(new Label { Text = "Titre *", Location = new Point(12, 12), AutoSize = true });
            txtTitre.SetBounds(90, 9, 880, 26);
            txtTitre.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtTitre.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            haut.Controls.Add(txtTitre);
            haut.Controls.Add(new Label { Text = "Projet", Location = new Point(12, 46), AutoSize = true });
            cboProjet.DropDownStyle = ComboBoxStyle.DropDown;
            cboProjet.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cboProjet.AutoCompleteSource = AutoCompleteSource.ListItems;
            cboProjet.Items.Add(OutilsProc.General);
            foreach (var pr in projets) cboProjet.Items.Add(pr);
            cboProjet.SetBounds(90, 43, 360, 26);
            haut.Controls.Add(cboProjet);
            haut.Controls.Add(new Label { Text = "(laisser « Général » pour une procédure commune à tous les projets)", Location = new Point(460, 47), AutoSize = true, ForeColor = Color.Gray });

            // Panneau de droite : tâches concernées
            var droite = new Panel { Dock = DockStyle.Right, Width = 300, Padding = new Padding(6, 4, 12, 4) };
            var lblLiens = new Label { Text = "TÂCHES CONCERNÉES", Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.FromArgb(45, 62, 80), Padding = new Padding(6, 0, 0, 0) };
            lstLiens.Dock = DockStyle.Fill;
            lstLiens.IntegralHeight = false;
            lstLiens.HorizontalScrollbar = true;
            var btns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 118, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 6, 0, 0) };
            btns.Controls.Add(BoutonLarge("Lier des tâches existantes…", delegate { LierExistantes(); }));
            btns.Controls.Add(BoutonLarge("Ajouter une tâche (texte libre)…", delegate { AjouterLibre(); }));
            btns.Controls.Add(BoutonLarge("Retirer la tâche sélectionnée", delegate { RetirerLien(); }));
            droite.Controls.Add(lstLiens);
            droite.Controls.Add(btns);
            droite.Controls.Add(lblLiens);

            // Barre de mise en forme
            var mef = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top, Padding = new Padding(8, 2, 0, 2) };
            mef.Items.Add(BoutonMef("G", "Gras (Ctrl+G)", new Font("Segoe UI", 9.5f, FontStyle.Bold), delegate { Basculer(FontStyle.Bold); }));
            mef.Items.Add(BoutonMef("I", "Italique (Ctrl+I)", new Font("Segoe UI", 9.5f, FontStyle.Italic), delegate { Basculer(FontStyle.Italic); }));
            mef.Items.Add(BoutonMef("S", "Souligné (Ctrl+U)", new Font("Segoe UI", 9.5f, FontStyle.Underline), delegate { Basculer(FontStyle.Underline); }));
            mef.Items.Add(new ToolStripSeparator());
            mef.Items.Add(BoutonMef("Titre", "Transformer la ligne en titre de section", null, delegate { Titre(); }));
            mef.Items.Add(BoutonMef("Texte normal", "Revenir au texte normal", null, delegate { Normal(); }));
            mef.Items.Add(BoutonMef("• Puces", "Liste à puces", null, delegate { rtb.SelectionBullet = !rtb.SelectionBullet; }));
            mef.Items.Add(new ToolStripSeparator());
            mef.Items.Add(BoutonMef("Surligner", "Surligner en jaune", null, delegate { Surligner(Color.FromArgb(255, 240, 120)); }));
            mef.Items.Add(BoutonMef("Rouge", "Texte en rouge (attention)", null, delegate { rtb.SelectionColor = rtb.SelectionColor == Color.Firebrick ? Color.Black : Color.Firebrick; }));
            mef.Items.Add(BoutonMef("Effacer surlignage", "Retirer le surlignage", null, delegate { Surligner(Color.White); }));

            rtb.Dock = DockStyle.Fill;
            rtb.Font = new Font("Segoe UI", 10.5f);
            rtb.AcceptsTab = true;
            rtb.DetectUrls = true;
            rtb.BorderStyle = BorderStyle.FixedSingle;
            rtb.LinkClicked += delegate(object s, LinkClickedEventArgs e) { try { System.Diagnostics.Process.Start(e.LinkText); } catch { } };

            var centre = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 0, 4) };
            centre.Controls.Add(rtb);
            centre.Controls.Add(mef);

            // Boutons
            var bas = new Panel { Dock = DockStyle.Bottom, Height = 52, Width = 1000 };
            var ok = new Button { Text = "Enregistrer" };
            var annuler = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel };
            ok.SetBounds(ClientSize.Width - 234, 10, 106, 32); annuler.SetBounds(ClientSize.Width - 120, 10, 106, 32);
            ok.Anchor = annuler.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            ok.Click += Valider;
            bas.Controls.Add(ok); bas.Controls.Add(annuler);
            CancelButton = annuler;
            var info = new Label { Text = "Ctrl+S : enregistrer", Location = new Point(14, 18), AutoSize = true, ForeColor = Color.Gray };
            bas.Controls.Add(info);

            Controls.Add(centre);
            Controls.Add(droite);
            Controls.Add(haut);
            Controls.Add(bas);

            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (!e.Control) return;
                if (e.KeyCode == Keys.S) { Valider(null, null); e.Handled = e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.G) { Basculer(FontStyle.Bold); e.Handled = e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.I) { Basculer(FontStyle.Italic); e.Handled = e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.U) { Basculer(FontStyle.Underline); e.Handled = e.SuppressKeyPress = true; }
            };

            // Valeurs
            txtTitre.Text = p.Titre;
            cboProjet.Text = string.IsNullOrEmpty(p.Projet) ? OutilsProc.General : p.Projet;
            Load += delegate
            {
                if (!string.IsNullOrEmpty(p.Rtf)) { try { rtb.Rtf = p.Rtf; } catch { rtb.Text = p.Texte; } }
                else AppliquerModele();
            };
            MajLiens();
            Shown += delegate { if (txtTitre.Text.Length == 0) txtTitre.Focus(); else rtb.Focus(); };
        }

        Button BoutonLarge(string t, EventHandler h)
        {
            var b = new Button { Text = t, Width = 280, Height = 32, Margin = new Padding(0, 0, 0, 4), TextAlign = ContentAlignment.MiddleLeft };
            b.Click += h; return b;
        }

        ToolStripButton BoutonMef(string t, string aide, Font f, EventHandler h)
        {
            var b = new ToolStripButton(t) { ToolTipText = aide, DisplayStyle = ToolStripItemDisplayStyle.Text };
            if (f != null) b.Font = f;
            b.Click += h; return b;
        }

        void AppliquerModele()
        {
            rtb.Text = Modele;
            foreach (var titre in new[] { "Objectif", "Prérequis", "Étapes", "Points de vigilance", "Contacts / services concernés" })
            {
                int i = rtb.Find(titre, RichTextBoxFinds.MatchCase);
                if (i < 0) continue;
                rtb.Select(i, titre.Length);
                rtb.SelectionFont = new Font("Segoe UI", 12.5f, FontStyle.Bold);
                rtb.SelectionColor = Color.FromArgb(45, 62, 80);
            }
            rtb.Select(0, 0);
        }

        void Basculer(FontStyle st)
        {
            var f = rtb.SelectionFont ?? rtb.Font;
            rtb.SelectionFont = new Font(f, f.Style ^ st);
            rtb.Focus();
        }

        void SelectionnerLigne()
        {
            if (rtb.SelectionLength > 0) return;
            int ligne = rtb.GetLineFromCharIndex(rtb.SelectionStart);
            int debut = rtb.GetFirstCharIndexFromLine(ligne);
            int fin = ligne + 1 < rtb.Lines.Length ? rtb.GetFirstCharIndexFromLine(ligne + 1) - 1 : rtb.TextLength;
            rtb.Select(debut, Math.Max(0, fin - debut));
        }

        void Titre()
        {
            SelectionnerLigne();
            rtb.SelectionFont = new Font("Segoe UI", 12.5f, FontStyle.Bold);
            rtb.SelectionColor = Color.FromArgb(45, 62, 80);
            rtb.Focus();
        }

        void Normal()
        {
            rtb.SelectionFont = new Font("Segoe UI", 10.5f, FontStyle.Regular);
            rtb.SelectionColor = Color.Black;
            rtb.SelectionBackColor = Color.White;
            rtb.Focus();
        }

        void Surligner(Color c) { rtb.SelectionBackColor = c; rtb.Focus(); }

        void MajLiens()
        {
            lstLiens.Items.Clear();
            foreach (var l in liens) lstLiens.Items.Add(OutilsProc.LibelleLien(l, donnees));
            if (liens.Count == 0) lstLiens.Items.Add("(aucune — facultatif)");
        }

        string ProjetSaisi()
        {
            string s = cboProjet.Text.Trim();
            return (s.Length == 0 || s == OutilsProc.General) ? "" : s;
        }

        void LierExistantes()
        {
            if (donnees.Taches.Count == 0)
            {
                MessageBox.Show(this, "Le module Tâches ne contient encore aucune tâche. Utilisez « Ajouter une tâche (texte libre) ».", "Procédure");
                return;
            }
            using (var f = new FormChoixTaches(donnees, ProjetSaisi(), liens.Where(l => l.TacheId != Guid.Empty).Select(l => l.TacheId)))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                liens.RemoveAll(l => l.TacheId != Guid.Empty && donnees.Taches.Any(t => t.Id == l.TacheId) && !f.Coches.Contains(l.TacheId));
                foreach (var id in f.Coches)
                    if (!liens.Any(l => l.TacheId == id))
                    {
                        var t = donnees.Taches.First(x => x.Id == id);
                        liens.Add(new LienTache { TacheId = id, Libelle = t.Titre });
                    }
            }
            MajLiens();
        }

        void AjouterLibre()
        {
            using (var d = new DialogueChoix("Tâche concernée", "Nom de la tâche (elle n'a pas besoin d'exister dans le module Tâches) :", new string[0], ""))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                liens.Add(new LienTache { TacheId = Guid.Empty, Libelle = d.Valeur });
            }
            MajLiens();
        }

        void RetirerLien()
        {
            int i = lstLiens.SelectedIndex;
            if (i < 0 || i >= liens.Count) return;
            liens.RemoveAt(i);
            MajLiens();
        }

        void Valider(object sender, EventArgs e)
        {
            if (txtTitre.Text.Trim().Length == 0)
            {
                MessageBox.Show(this, "Le titre est obligatoire.", "Procédure", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtTitre.Focus(); return;
            }
            Proc.Titre = txtTitre.Text.Trim();
            string pr = ProjetSaisi();
            foreach (string item in cboProjet.Items)
                if (string.Equals(item, pr, StringComparison.CurrentCultureIgnoreCase)) { pr = item; break; }
            Proc.Projet = pr;
            Proc.Rtf = rtb.Rtf;
            Proc.Texte = rtb.Text;
            Proc.Taches = liens;
            Proc.Modification = DateTime.Now;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // ---------------------------------------------------------------- Onglet Procédures
    public class PanneauProcedures : UserControl
    {
        Donnees donnees;
        public Action Sauver;
        public Func<List<string>> ProjetsConnus;
        public Action ApresModification;

        DataGridView grille = new DataGridView();
        ComboBox cboProjet = new ComboBox();
        TextBox txtRecherche = new TextBox();
        RichTextBox apercu = new RichTextBox();
        Label lblTitre = new Label();
        Label lblInfos = new Label();
        Guid? filtreTache;
        Label lblFiltreTache = new Label();
        LinkLabel lnkRetirerFiltre = new LinkLabel();

        public PanneauProcedures(Donnees d)
        {
            donnees = d;
            Font = new Font("Segoe UI", 9.5f);

            var outils = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8, 8, 8, 4), WrapContents = false };
            outils.Controls.Add(Bouton("Nouvelle procédure", 150, delegate { Nouvelle(null, null); }));
            outils.Controls.Add(Bouton("Modifier", 90, delegate { Modifier(); }));
            outils.Controls.Add(Bouton("Dupliquer", 90, delegate { Dupliquer(); }));
            outils.Controls.Add(Bouton("Supprimer", 95, delegate { Supprimer(); }));
            outils.Controls.Add(Bouton("Exporter (Word/RTF)", 150, delegate { var p = Selection(); if (p != null) OutilsProc.Exporter(FindForm(), p, donnees); }));
            outils.Controls.Add(new Label { Text = "  Projet :", AutoSize = true, Margin = new Padding(8, 7, 0, 0) });
            cboProjet.DropDownStyle = ComboBoxStyle.DropDownList; cboProjet.Width = 190; cboProjet.Margin = new Padding(3, 4, 3, 0);
            cboProjet.SelectedIndexChanged += delegate { Rafraichir(); };
            outils.Controls.Add(cboProjet);
            outils.Controls.Add(new Label { Text = "Rechercher :", AutoSize = true, Margin = new Padding(8, 7, 0, 0) });
            txtRecherche.Width = 180; txtRecherche.Margin = new Padding(3, 4, 3, 0);
            txtRecherche.TextChanged += delegate { Rafraichir(); };
            outils.Controls.Add(txtRecherche);
            lblFiltreTache.AutoSize = true; lblFiltreTache.Margin = new Padding(12, 7, 0, 0);
            lblFiltreTache.ForeColor = Color.FromArgb(0, 90, 170); lblFiltreTache.Font = new Font(Font, FontStyle.Bold);
            lnkRetirerFiltre.Text = "(afficher tout)"; lnkRetirerFiltre.AutoSize = true; lnkRetirerFiltre.Margin = new Padding(4, 7, 0, 0);
            lnkRetirerFiltre.LinkClicked += delegate { filtreTache = null; Rafraichir(); };
            outils.Controls.Add(lblFiltreTache); outils.Controls.Add(lnkRetirerFiltre);

            // Liste
            grille.Dock = DockStyle.Fill;
            grille.ReadOnly = true;
            grille.AllowUserToAddRows = false; grille.AllowUserToDeleteRows = false; grille.AllowUserToResizeRows = false;
            grille.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grille.MultiSelect = false;
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
            grille.Columns.Add(new DataGridViewTextBoxColumn { Name = "Titre", HeaderText = "Procédure", FillWeight = 45 });
            grille.Columns.Add(new DataGridViewTextBoxColumn { Name = "Projet", HeaderText = "Projet", FillWeight = 25 });
            grille.Columns.Add(new DataGridViewTextBoxColumn { Name = "Taches", HeaderText = "Tâches", FillWeight = 10 });
            grille.Columns.Add(new DataGridViewTextBoxColumn { Name = "Modif", HeaderText = "Mise à jour", FillWeight = 16 });
            foreach (DataGridViewColumn c in grille.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
            grille.SelectionChanged += delegate { MajApercu(); };
            grille.CellDoubleClick += delegate(object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) Modifier(); };
            grille.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { Modifier(); e.Handled = true; }
                else if (e.KeyCode == Keys.Delete) { Supprimer(); e.Handled = true; }
            };
            var ctx = new ContextMenuStrip();
            ctx.Items.Add("Ouvrir / modifier…", null, delegate { Modifier(); });
            ctx.Items.Add("Dupliquer", null, delegate { Dupliquer(); });
            ctx.Items.Add("Exporter en RTF (Word)…", null, delegate { var p = Selection(); if (p != null) OutilsProc.Exporter(FindForm(), p, donnees); });
            ctx.Items.Add(new ToolStripSeparator());
            ctx.Items.Add("Supprimer", null, delegate { Supprimer(); });
            grille.ContextMenuStrip = ctx;
            grille.CellMouseDown += delegate(object s, DataGridViewCellMouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0) { grille.ClearSelection(); grille.Rows[e.RowIndex].Selected = true; }
            };

            // Aperçu
            var droite = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 6), BackColor = Color.White };
            lblTitre.Dock = DockStyle.Top; lblTitre.Height = 34; lblTitre.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
            lblTitre.ForeColor = Color.FromArgb(45, 62, 80);
            lblInfos.Dock = DockStyle.Top; lblInfos.Height = 52; lblInfos.ForeColor = Color.DimGray;
            apercu.Dock = DockStyle.Fill; apercu.ReadOnly = true; apercu.BorderStyle = BorderStyle.None; apercu.BackColor = Color.White;
            apercu.Font = new Font("Segoe UI", 10.5f);
            apercu.DetectUrls = true;
            apercu.LinkClicked += delegate(object s, LinkClickedEventArgs e) { try { System.Diagnostics.Process.Start(e.LinkText); } catch { } };
            droite.Controls.Add(apercu); droite.Controls.Add(lblInfos); droite.Controls.Add(lblTitre);

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = 6 };
            split.Panel1.Controls.Add(grille);
            split.Panel2.Controls.Add(droite);
            Load += delegate { try { split.SplitterDistance = Math.Max(300, Width * 45 / 100); } catch { } };

            Controls.Add(split);
            Controls.Add(outils);
        }

        Button Bouton(string texte, int largeur, EventHandler clic)
        {
            var b = new Button { Text = texte, Width = largeur, Height = 30, Margin = new Padding(0, 0, 6, 0) };
            b.Click += clic; return b;
        }

        public void MajProjets()
        {
            string courant = cboProjet.SelectedItem as string;
            cboProjet.Items.Clear();
            cboProjet.Items.Add("Tous");
            cboProjet.Items.Add(OutilsProc.General);
            foreach (var p in ProjetsConnus()) cboProjet.Items.Add(p);
            int i = courant == null ? 0 : cboProjet.Items.IndexOf(courant);
            cboProjet.SelectedIndex = i < 0 ? 0 : i;
        }

        public void FiltrerSurTache(Tache t)
        {
            filtreTache = t.Id;
            cboProjet.SelectedIndex = 0;
            txtRecherche.Text = "";
            Rafraichir();
        }

        public void SelectionnerProjet(string projet)
        {
            int i = projet == null ? 0 : cboProjet.Items.IndexOf(projet);
            if (i >= 0) cboProjet.SelectedIndex = i;
        }

        Procedure Selection()
        {
            if (grille.SelectedRows.Count == 0) return null;
            return grille.SelectedRows[0].Tag as Procedure;
        }

        public void Rafraichir()
        {
            var sel = Selection();
            Guid? idSel = sel == null ? (Guid?)null : sel.Id;
            IEnumerable<Procedure> q = donnees.Procedures;
            if (cboProjet.SelectedIndex == 1) q = q.Where(p => string.IsNullOrEmpty(p.Projet));
            else if (cboProjet.SelectedIndex > 1)
            {
                string pr = (string)cboProjet.SelectedItem;
                // les procédures générales restent visibles dans chaque projet
                q = q.Where(p => string.IsNullOrEmpty(p.Projet) || string.Equals(p.Projet, pr, StringComparison.CurrentCultureIgnoreCase));
            }
            if (filtreTache.HasValue)
            {
                var id = filtreTache.Value;
                q = q.Where(p => p.ConcerneTache(id));
                var t = donnees.Taches.FirstOrDefault(x => x.Id == id);
                lblFiltreTache.Text = "Liées à : " + (t != null ? t.Titre : "?");
            }
            lblFiltreTache.Visible = lnkRetirerFiltre.Visible = filtreTache.HasValue;
            string r = txtRecherche.Text.Trim();
            if (r.Length > 0)
                q = q.Where(p => (p.Titre + " " + p.Projet + " " + p.Texte + " " + string.Join(" ", p.Taches.Select(l => OutilsProc.LibelleLien(l, donnees)).ToArray()))
                    .IndexOf(r, StringComparison.CurrentCultureIgnoreCase) >= 0);

            grille.Rows.Clear();
            foreach (var p in q.OrderBy(p => string.IsNullOrEmpty(p.Projet) ? 1 : 0).ThenBy(p => p.Projet).ThenBy(p => p.Titre))
            {
                int i = grille.Rows.Add(p.Titre, OutilsProc.NomProjet(p), p.Taches.Count == 0 ? "" : p.Taches.Count.ToString(), p.Modification.ToString("dd/MM/yyyy"));
                grille.Rows[i].Tag = p;
                if (string.IsNullOrEmpty(p.Projet)) grille.Rows[i].Cells["Projet"].Style.ForeColor = Color.Gray;
                if (idSel.HasValue && p.Id == idSel.Value) grille.Rows[i].Selected = true;
            }
            if (!idSel.HasValue && grille.Rows.Count > 0) { grille.ClearSelection(); grille.Rows[0].Selected = true; }
            MajApercu();
        }

        void MajApercu()
        {
            var p = Selection();
            if (p == null)
            {
                lblTitre.Text = donnees.Procedures.Count == 0 ? "Aucune procédure" : "";
                lblInfos.Text = donnees.Procedures.Count == 0 ? "Cliquez sur « Nouvelle procédure » pour rédiger votre première procédure." : "";
                apercu.Clear();
                return;
            }
            lblTitre.Text = p.Titre;
            string taches = p.Taches.Count == 0 ? "—" : string.Join(" ; ", p.Taches.Select(l => OutilsProc.LibelleLien(l, donnees)).ToArray());
            lblInfos.Text = "Projet : " + OutilsProc.NomProjet(p) + "     •     Mise à jour : " + p.Modification.ToString("dd/MM/yyyy HH:mm") +
                            "\nTâches concernées : " + taches;
            try { apercu.Rtf = p.Rtf; } catch { apercu.Text = p.Texte; }
        }

        void Apres()
        {
            if (Sauver != null) Sauver();
            if (ApresModification != null) ApresModification();
            MajProjets();
            Rafraichir();
        }

        public void Nouvelle(string projet, IEnumerable<Tache> taches)
        {
            var p = new Procedure();
            if (projet == null && cboProjet.SelectedIndex > 1) projet = (string)cboProjet.SelectedItem;
            p.Projet = projet ?? "";
            if (taches != null)
                foreach (var t in taches) p.Taches.Add(new LienTache { TacheId = t.Id, Libelle = t.Titre });
            using (var f = new FormProcedure(p, donnees, ProjetsConnus()))
                if (f.ShowDialog(FindForm()) != DialogResult.OK) return;
            donnees.Procedures.Add(p);
            MemoriserProjet(p);
            Apres();
            Selectionner(p);
        }

        public void Ouvrir(Procedure p)
        {
            using (var f = new FormProcedure(p, donnees, ProjetsConnus()))
                if (f.ShowDialog(FindForm()) != DialogResult.OK) return;
            MemoriserProjet(p);
            Apres();
            Selectionner(p);
        }

        void Selectionner(Procedure p)
        {
            foreach (DataGridViewRow r in grille.Rows)
                if (r.Tag == p) { grille.ClearSelection(); r.Selected = true; grille.FirstDisplayedScrollingRowIndex = r.Index; break; }
        }

        void MemoriserProjet(Procedure p)
        {
            if (!string.IsNullOrEmpty(p.Projet) && !donnees.Projets.Any(x => string.Equals(x, p.Projet, StringComparison.CurrentCultureIgnoreCase)))
                donnees.Projets.Add(p.Projet);
        }

        public void Modifier() { var p = Selection(); if (p != null) Ouvrir(p); }

        void Dupliquer()
        {
            var s = Selection();
            if (s == null) return;
            var p = new Procedure
            {
                Titre = s.Titre + " (copie)", Projet = s.Projet, Rtf = s.Rtf, Texte = s.Texte,
                Taches = s.Taches.Select(l => new LienTache { TacheId = l.TacheId, Libelle = l.Libelle }).ToList()
            };
            using (var f = new FormProcedure(p, donnees, ProjetsConnus()))
                if (f.ShowDialog(FindForm()) != DialogResult.OK) return;
            donnees.Procedures.Add(p);
            MemoriserProjet(p);
            Apres();
            Selectionner(p);
        }

        void Supprimer()
        {
            var p = Selection();
            if (p == null) return;
            if (MessageBox.Show(FindForm(), "Supprimer la procédure « " + p.Titre + " » ?", "Confirmation",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            donnees.Procedures.Remove(p);
            Apres();
        }
    }
}
