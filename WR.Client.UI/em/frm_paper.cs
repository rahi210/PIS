﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Threading;
using System.Text;

using WR.Client.Utils;
using WR.Client.WCF;
using WR.WCF.Contract;
using WR.WCF.DataContract;
using WR.Client.Controls;
using WR.Utils;
using System.Collections;

namespace WR.Client.UI
{
    public partial class frm_paper : FormBase
    {
        //private Brush _bgColor = new SolidBrush(Color.DarkBlue);
        private Brush _bgColor = new SolidBrush(SystemColors.ControlDarkDark);
        private Brush _egPen = new SolidBrush(SystemColors.Control);
        private Pen _linePen = new Pen(Color.White);

        private Brush _dPen = new SolidBrush(Color.Black);//new SolidBrush(Color.DarkGreen);
        private Brush _lPen = new SolidBrush(Color.DarkGreen);//new SolidBrush(Color.ForestGreen);
        private Brush _rPen = new SolidBrush(Color.Red);

        private string[] _oparams;
        /// <summary>
        /// 参数
        /// </summary>
        public string[] Oparams
        {
            get { return _oparams; }
            set { _oparams = value; }
        }

        private string _schemeid;
        /// <summary>
        /// 缺陷分类ID
        /// </summary>
        public string Schemeid
        {
            get { return _schemeid; }
            set { _schemeid = value; }
        }

        private string _resultid;// = "0a86e51c-3b6d-49f8-9815-df5555cb9a40";
        /// <summary>
        /// 晶片检测ID
        /// </summary>
        public string Resultid
        {
            get { return _resultid; }
            set { _resultid = value; }
        }

        public bool IsFinished { get; set; }

        /// <summary>
        /// 缺陷列表
        /// </summary>
        private List<EmdefectlistEntity> _defectlist;
        /// <summary>
        /// Layout信息
        /// </summary>
        private List<WmdielayoutlistEntitiy> _dielayoutlist;

        private EmExamResultEntity examResult;
        private EMPLAN emPlan;
        private string showMode;

        public bool IsLayoutRole { get; set; }
        public bool IsSave { get; set; }

        private DateTime lastRunTime;
        //private LoggerEx log = null;
        private CheckBoxComboBox tlsNewClass;

        public frm_paper()
        {
            tlsNewClass = new CheckBoxComboBox();
            tlsNewClass.FlatStyle = FlatStyle.Popup;
            tlsNewClass.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            tlsNewClass.Size = new System.Drawing.Size(100, 25);
            tlsNewClass.CheckBoxCheckedChanged += new System.EventHandler(this.tlsNewClass_CheckBoxCheckedChanged);
            tlsNewClass.DropDownStyle = ComboBoxStyle.DropDownList;

            InitializeComponent();

            ToolStripControlHost host = new ToolStripControlHost(tlsNewClass);
            toolStrip1.Items.Add(host);


            grdData.AutoGenerateColumns = false;
            PicShow.WrImage = null;

            picWafer.DefectList = new List<Controls.DefectCoordinate>();

            //GetLayout();
        }

        private void frm_preview_Load(object sender, EventArgs e)
        {
            grdData.Visible = true;
            lstView.Visible = false;
            grdData.Dock = DockStyle.Fill;
            lstView.Dock = DockStyle.Fill;

            if (Oparams != null && Oparams.Length > 2)
            {
                Resultid = Oparams[0];
                showMode = Oparams[1];
                //lblWaferID.Text = string.Format("Lot:{0}  Wafer:{1} Defect Die:{2} Yield:{3}", Oparams[1], Oparams[2], Oparams[3], Oparams[4]);
            }

            //InitData();
            tlsStatus.SelectedIndex = 0;

            timer1.Enabled = true;

            //tlsClass.Visible = false;

            //判断用户是否有权限变更布局
            IsLayoutRole = DataCache.Tbmenus.Count(s => s.MENUCODE == "40003") > 0;

            if (!IsLayoutRole)
            {
                splitter1.Enabled = false;
                //splitter2.Enabled = false;
                splitter3.Enabled = false;
            }

            //panel2.Width = Convert.ToInt32(panel4.Height * 1.25);

            lastRunTime = DateTime.Now;

            //log = LogService.Getlog(this.GetType());

            IsSave = true;
        }

        /// <summary>
        /// 获取页面布局
        /// </summary>
        private void GetLayout()
        {
            var layout = WR.Utils.Config.GetAppSetting("previewLayout");

            if (!string.IsNullOrEmpty(layout))
            {
                var controlsArray = layout.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToArray();

                foreach (var control in controlsArray)
                {
                    var param = control.Split(':');
                    var width = int.Parse(param[1].Split(',')[0]);
                    var height = int.Parse(param[1].Split(',')[1]);

                    switch (param[0])
                    {
                        case "pnlPic":
                            pnlPic.Width = width;
                            pnlPic.Height = height;
                            break;
                        case "panel1":
                            panel1.Width = width;
                            panel1.Height = height;
                            break;
                        case "panel2":
                            panel2.Width = width;
                            panel2.Height = height;
                            break;
                        case "panel4":
                            panel4.Width = width;
                            panel4.Height = height;
                            break;
                        case "tabControl1":
                            tabControl1.Width = width;
                            tabControl1.Height = height;
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private bool freshing = false;
        private bool hasDraw = true;

        /// <summary>
        /// 初始化缺陷类型列表
        /// </summary>
        private void InitClassList()
        {

            tlsClass.SelectedIndexChanged -= new System.EventHandler(this.tlsClass_SelectedIndexChanged);

            var classList = (from c in _defectlist
                             orderby c.Cclassid
                             group c by new { c.Cclassid, c.Description } into g
                             select new ClassDropDownModel { Cclassid = g.Key.Cclassid, Description = g.Key.Description }).ToList();

            //classList.Insert(0, new ClassDropDownModel { Cclassid = -1, Description = "All" });

            tlsClass.ComboBox.DisplayMember = "Description";
            tlsClass.ComboBox.ValueMember = "Cclassid";
            tlsClass.ComboBox.DataSource = classList;

            tlsClass.SelectedIndexChanged += new System.EventHandler(this.tlsClass_SelectedIndexChanged);

            tlsClass.Visible = false;

            //多选
            //tlsNewClass.SelectedIndexChanged -= new System.EventHandler(this.tlsNewClass_SelectedIndexChanged);

            tlsNewClass.DataSource = new ListSelectionWrapper<ClassDropDownModel>(classList, "Description");
            tlsNewClass.DisplayMemberSingleItem = "Name";
            tlsNewClass.DisplayMember = "NameConcatenated";
            tlsNewClass.ValueMember = "Selected";

            //tlsNewClass.SelectedIndexChanged += new System.EventHandler(this.tlsNewClass_SelectedIndexChanged);
        }

        /// <summary>
        /// 数据加载
        /// </summary>
        private void InitData()
        {
            if (freshing)
                return;

            freshing = true;

            ShowLoading();

            //重绘图片
            if (hasDraw)
            {
                if (picWafer.DefectList.Count > 0)
                {
                    picWafer.DefectList.Clear();
                    picWafer.scaleX = 1;
                    picWafer.scaleY = 1;
                }

                picWafer.WrImage = null;
            }

            tlsSaveResult.Enabled = false;
            tlsFinish.Enabled = false;
            tlsReclass.Enabled = false;
            tlsReclass.DropDownItems.Clear();

            Thread thr = new Thread(new ThreadStart(() =>
            {
                try
                {
                    //获取缺陷列表
                    IwrService service = wrService.GetService();

                    if (showMode == "0")
                    {
                        if (this.InvokeRequired)
                        {
                            this.Invoke(new Action(() =>
                            {
                                //tlsSaveResult.Enabled = true;
                                grdData.Columns["CorrectAnswer"].Visible = true;
                            }));
                        }
                        else
                        {
                            //tlsSaveResult.Enabled = true;
                            grdData.Columns["CorrectAnswer"].Visible = true;
                        }
                    }
                    else
                    {
                        var userid = string.IsNullOrEmpty(Resultid) ? DataCache.UserInfo.ID : string.Empty;

                        var emResultList = service.GetExamResult(userid, Resultid);

                        examResult = emResultList.FirstOrDefault(s => s.ENDDATE == null);

                        if (examResult == null)
                        {
                            examResult = emResultList.FirstOrDefault();

                            if (examResult == null)
                                return;
                        }

                        Resultid = examResult.EID;

                        if (this.InvokeRequired)
                        {
                            this.Invoke(new Action(() =>
                            {
                                if (examResult.ENDDATE == null)
                                {
                                    if (showMode != "1")
                                    {
                                        tlsSaveResult.Enabled = true;
                                        tlsFinish.Enabled = true;
                                        tlsReclass.Enabled = true;

                                        timer4.Enabled = true;
                                    }
                                }
                                else
                                {
                                    grdData.Columns["CorrectAnswer"].Visible = true;

                                    lblWaferID.Text = string.Format("Score:{0}  Right Number:{1} Defect Number:{2}", examResult.TOTALSCORE, examResult.RIGHTNUM, examResult.NUMDEFECT);
                                }
                            }));
                        }
                        else
                        {
                            if (examResult.ENDDATE == null)
                            {
                                if (showMode != "1")
                                {
                                    tlsSaveResult.Enabled = true;
                                    tlsFinish.Enabled = true;
                                    tlsReclass.Enabled = true;

                                    timer4.Enabled = true;
                                }
                            }
                            else
                            {
                                grdData.Columns["CorrectAnswer"].Visible = true;
                                lblWaferID.Text = string.Format("Score:{0}  Right Number:{1} Defect Number:{2}", examResult.TOTALSCORE, examResult.RIGHTNUM, examResult.NUMDEFECT);
                            }
                        }

                    }

                    string sts = "";
                    if (this.InvokeRequired)
                        this.Invoke(new Action(() => { sts = GetStatus(); }));
                    else
                        sts = GetStatus();

                    var defList = service.GetPaperDefectList(Resultid, sts).OrderBy(s => s.ImageName).ToList();
                    //var defList = service.GetDefectList(Resultid, sts).ToList();
                    _defectlist = defList;

                    if (this.InvokeRequired)
                        this.Invoke(new Action(() => { InitClassList(); }));
                    else
                        InitClassList();

                    //var wf = DataCache.WaferResultInfo.FirstOrDefault(p => p.RESULTID == Resultid);
                    //if (wf == null)
                    //    return;

                    //_dielayoutlist = service.GetDielayoutListById(wf.DIELAYOUTID);

                    List<CMNDICT> hotkey = DataCache.CmnDict.Where(p => p.DICTID == "2010").ToList();
                    hotkey.Add(new CMNDICT() { DICTID = "2010", CODE = null, NAME = "-" });

                    if (this.InvokeRequired)
                        this.Invoke(new Action(() =>
                        {
                            colHotKey.DisplayMember = "NAME";
                            colHotKey.ValueMember = "CODE";
                            colHotKey.DataSource = hotkey;
                        }));
                    else
                    {
                        colHotKey.DisplayMember = "NAME";
                        colHotKey.ValueMember = "CODE";
                        colHotKey.DataSource = hotkey;
                    }

                    //查询缺陷分类
                    //Schemeid = "151936ae-ce1f-4950-9e5f-2a9a687fd1ca";
                    var schemeList = service.GetClassificationItemByResultId(Resultid);

                    if (schemeList.Count <= 0)
                        return;

                    Schemeid = schemeList[0].SCHEMEID;
                    var clst = service.GetClassificationItem(Schemeid, DataCache.UserInfo.ID).OrderBy(p => p.ID).ToList();

                    //过滤没有权限的缺陷分类
                    var classificationRoleCnt = DataCache.Tbmenus.Count(s => s.MENUCODE == "40001");

                    if (classificationRoleCnt > 0)
                    {
                        var forbidClassificationItem = DataCache.CmnDict.Where(s => s.DICTID == "3000").Select(s => s.CODE).ToList();

                        clst = clst.Where(s => !forbidClassificationItem.Contains(s.ID.ToString())).ToList();
                    }

                    if (this.InvokeRequired)
                        this.Invoke(new Action(() =>
                        {
                            clst.ForEach((p) =>
                            {
                                ToolStripItem itm = tlsReclass.DropDownItems.Add(string.Format("{0} {1}", p.ID, p.NAME));
                                itm.Tag = p.ITEMID;
                                itm.Click += new EventHandler(itm_Click);
                            });

                            grdClass.DataSource = clst;
                            tabControl1_SelectedIndexChanged(null, null);
                        }));
                    else
                    {
                        clst.ForEach((p) =>
                        {
                            ToolStripItem itm = tlsReclass.DropDownItems.Add(string.Format("{0} {1}", p.ID, p.NAME));
                            itm.Tag = p.ITEMID;
                            itm.Click += new EventHandler(itm_Click);
                        });
                        grdClass.DataSource = clst;
                        tabControl1_SelectedIndexChanged(null, null);
                    }

                    if (this.InvokeRequired)
                        this.Invoke(new Action(() =>
                        {
                            //grdData.DataSource = new BindingCollection<EmdefectlistEntity>(defList);
                            grdData.DataSource = defList;
                            lstView.VirtualMode = true;
                            lstView.VirtualListSize = defList.Count;

                            //tabControl1_SelectedIndexChanged(null, null);

                            //if (wf.ISCHECKED != "2")
                            //{
                            //    tlsSaveResult.Enabled = true;
                            //    tlsFinish.Enabled = true;
                            //    tlsReclass.Enabled = true;
                            //}

                            if (!grdData.Visible)
                            {
                                lstView.Focus();
                                lstView.Items[0].Selected = true;
                                lstView.Items[0].Focused = true;
                                lstView.EnsureVisible(0);
                                //DrawDefect(defList[0].DieAddress);
                            }

                            if (defList.Count > 1)
                                DrawDefect(defList[0].DieAddress);
                        }));
                    else
                    {
                        //grdData.DataSource = new BindingCollection<EmdefectlistEntity>(defList);
                        grdData.DataSource = defList;
                        lstView.VirtualMode = true;
                        lstView.VirtualListSize = defList.Count;

                        //tabControl1_SelectedIndexChanged(null, null);

                        //if (wf.ISCHECKED != "2")
                        //{
                        //    tlsSaveResult.Enabled = true;
                        //    tlsFinish.Enabled = true;
                        //    tlsReclass.Enabled = true;
                        //}

                        if (!grdData.Visible && defList.Count > 1)
                        {
                            lstView.Focus();
                            lstView.Items[0].Selected = true;
                            lstView.Items[0].Focused = true;
                            lstView.EnsureVisible(0);
                            DrawDefect(defList[0].DieAddress);
                        }
                    }

                    if (defList.Count == 1)
                    {
                        if (this.InvokeRequired)
                            this.Invoke(new Action(() =>
                            {
                                DrawDefect(defList[0].DieAddress);
                            }));
                        else
                        {
                            DrawDefect(defList[0].DieAddress);
                        }
                    }
                    else if (defList.Count < 1)
                    {
                        if (this.InvokeRequired)
                            this.Invoke(new Action(() =>
                            {
                                DrawDefect("0,0");
                            }));
                        else
                        {
                            DrawDefect("0,0");
                        }
                    }

                    if (this.InvokeRequired)
                        this.Invoke(new Action(() =>
                        {
                            if (grdData.Visible)
                                grdData.Focus();
                            else if (lstView.Visible)
                                lstView.Focus();
                        }));
                    else
                    {
                        if (grdData.Visible)
                            grdData.Focus();
                        else if (lstView.Visible)
                            lstView.Focus();
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    MsgBoxEx.Error("An error occurred while attempting to load data");
                }
                finally
                {
                    CloseLoading();

                    freshing = false;
                }
            }));

            thr.IsBackground = true;
            thr.Start();
        }

        /// <summary>
        /// 重新定位class
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void itm_Click(object sender, EventArgs e)
        {
            try
            {
                var items = grdClass.DataSource as List<WMCLASSIFICATIONITEM>;
                if (items == null)
                    return;

                var cl = (ToolStripItem)sender;
                var itm = items.FirstOrDefault(p => p.ITEMID == cl.Tag.ToString());
                if (itm == null)
                    return;

                if (grdData.Visible)
                {
                    if (grdData.SelectedRows == null || grdData.SelectedRows.Count < 1)
                        return;

                    if (cnmReclass.Tag.ToString() == "2")
                    {
                        var list = grdData.DataSource as List<EmdefectlistEntity>;
                        foreach (var def in picWafer.SelectDefect)
                        {
                            var ent = list.FirstOrDefault(s => s.DieAddress == def.ToString() && s.Cclassid != itm.ID);

                            if (ent == null)
                                continue;

                            ent.Cclassid = itm.ID;
                            ent.InspclassifiId = itm.ITEMID;
                            ent.ModifiedDefect = ent.INSPID;
                            ent.Description = itm.NAME;

                            UpdateDefectClassification(ent);

                            var index = list.FindIndex(s => s.Id == ent.Id);
                            grdData.InvalidateRow(index);
                        }

                        InitClassList();
                        picWafer.Status = "";
                        DrawDefect(picWafer.CurrentDefect);
                        //grdData.CurrentCell = grdData[grdData.CurrentCell.ColumnIndex, grdData.CurrentCell.RowIndex + 1];
                    }
                    else
                    {
                        var ent = grdData.SelectedRows[0].DataBoundItem as EmdefectlistEntity;
                        if (ent == null)
                            return;

                        ent.Cclassid = itm.ID;
                        ent.InspclassifiId = itm.ITEMID;
                        ent.ModifiedDefect = ent.INSPID;
                        ent.Description = itm.NAME;

                        UpdateDefectClassification(ent);
                    }

                    grdData.InvalidateRow(grdData.SelectedRows[0].Index);
                    //DrawDefect(ent.DieAddress);
                }
                else
                {
                    if (lstView.SelectedIndices == null || lstView.SelectedIndices.Count < 1)
                        return;

                    if (cnmReclass.Tag.ToString() == "2")
                    {
                        var list = grdData.DataSource as List<EmdefectlistEntity>;
                        foreach (var def in picWafer.SelectDefect)
                        {
                            var ent = list.FirstOrDefault(s => s.DieAddress == def.ToString() && s.Cclassid != itm.ID);

                            if (ent == null)
                                return;

                            ent.Cclassid = itm.ID;
                            ent.InspclassifiId = itm.ITEMID;
                            ent.ModifiedDefect = ent.INSPID;
                            ent.Description = itm.NAME;

                            UpdateDefectClassification(ent);

                            lstView.RedrawItems(lstView.SelectedIndices[0], lstView.SelectedIndices[0], false);
                            DrawDefect(ent.DieAddress);
                        }
                    }
                    else
                    {
                        List<EmdefectlistEntity> list = grdData.DataSource as List<EmdefectlistEntity>;
                        var ent = list[lstView.SelectedIndices[0]];
                        ent.Cclassid = itm.ID;
                        ent.InspclassifiId = itm.ITEMID;
                        ent.ModifiedDefect = ent.INSPID;
                        ent.Description = itm.NAME;

                        UpdateDefectClassification(ent);

                        lstView.RedrawItems(lstView.SelectedIndices[0], lstView.SelectedIndices[0], false);
                        DrawDefect(ent.DieAddress);
                    }
                }

                tabControl1_SelectedIndexChanged(null, null);
            }
            catch (Exception ex)
            {
                log.Error(ex);
                MsgBoxEx.Error("An error occurred while re-classify");
            }
        }

        /// <summary>
        /// 加载缺陷图片
        /// </summary>
        /// <param name="filename"></param>
        private void GetImage(string filename)
        {
            try
            {
                if (string.IsNullOrEmpty(filename))
                    PicShow.WrImage = null;
                else
                {
                    IwrService service = wrService.GetService();
                    //Stream st = service.GetPic(Resultid + "\\" + filename);
                    Stream st = service.GetPic(filename);
                    Image pic = Image.FromStream(st, true);
                    PicShow.WrImage = pic;
                    PicShow.Tag = filename;
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                MsgBoxEx.Error("An error occurred while attempting to load image");
            }
        }

        /// <summary>
        /// 获取缺陷条目检测状态
        /// </summary>
        /// <returns></returns>
        private string GetStatus()
        {
            string res = "";
            if (tlsStatus.SelectedIndex == 1)
                res = "0";
            else if (tlsStatus.SelectedIndex == 2)
                res = "1";

            return res;
        }

        /// <summary>
        /// 切换状态
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tlsStatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tlsFilter.Checked)
                tlsFilter.Checked = false;

            InitData();

            //if (lstView.Visible && lstView.SelectedIndices != null && lstView.SelectedIndices.Count > 0)
            //{
            //    lstView.Items[lstView.SelectedIndices[0]].Selected = false;
            //}
        }

        System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
        string dieLoction = string.Empty;
        /// <summary>
        /// 选中行变化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void grdData_SelectionChanged(object sender, EventArgs e)
        {
            //log.Debug("SelectionChanged Start...............");
            if (grdData.SelectedRows != null && grdData.SelectedRows.Count > 0 && grdData.Visible)
            {
                timer2.Enabled = false;

                ResetTck();
                //log.Debug("GetImage Start...............");
                GetImage(grdData.SelectedRows[0].Cells["ColImageName"].Value as string);
                //picWafer.Invalidate();
                //log.Debug("GetImage End...............");
                var ent = grdData.SelectedRows[0].DataBoundItem as EmdefectlistEntity;

                stopWatch.Stop();
                //int seconds = (DateTime.Now - lastRunTime).Milliseconds;
                long seconds = stopWatch.ElapsedMilliseconds;

                //log.Debug(seconds);
                if (seconds > 1000)
                {
                    //log.Debug("DrawDefect Start...............");
                    DrawDefect(ent.DieAddress);

                    //log.Debug("DrawDefect End...............");
                    //var thr = new Thread(() =>
                    //{
                    //    if (this.InvokeRequired)
                    //    {
                    //        this.BeginInvoke(new Action(() =>
                    //        {
                    //            DrawDefect(ent.DieAddress);
                    //        }));
                    //    }
                    //    else
                    //    {
                    //        DrawDefect(ent.DieAddress);
                    //    }
                    //});
                    //thr.Start();
                }
                else
                {
                    dieLoction = ent.DieAddress;
                    if (seconds > 0)
                        timer3.Enabled = true;
                }

                lastRunTime = DateTime.Now;
                stopWatch.Restart();

                timer2.Enabled = true;
            }
            //log.Debug("SelectionChanged End...............");
        }

        /// <summary>
        /// 画图
        /// </summary>
        private void DrawDefect(string loction)
        {
            //if (_dielayoutlist == null || _dielayoutlist.Count < 1)
            //    return;

            //int col = _dielayoutlist[0].COLUMNS_;
            //int row = _dielayoutlist[0].ROWS_;

            //var listDieLayout = _dielayoutlist.Select(s => new DieLayout { X = s.DIEADDRESSX, Y = s.DIEADDRESSY, FillColor = s.DISPOSITION.Trim() == "NotProcess" ? Color.Gray.Name : "" })
            //.ToList<DieLayout>(); ;

            //var items = grdClass.DataSource as List<WMCLASSIFICATIONITEM>;

            //var defectlist = new List<EmdefectlistEntity>();
            ////var classId = Convert.ToInt32(tlsClass.ComboBox.SelectedValue);
            //var classId = -1;

            //if (tlsClass.Visible)
            //{
            //    if (tlsClass.ComboBox.SelectedValue != null)
            //        classId = Convert.ToInt32(tlsClass.ComboBox.SelectedValue);

            //    if (classId != -1)
            //        defectlist = grdData.DataSource as List<EmdefectlistEntity>;
            //    else
            //        defectlist = _defectlist;
            //}

            //if (!string.IsNullOrEmpty(tlsNewClass.Text))
            //    defectlist = grdData.DataSource as List<EmdefectlistEntity>;
            //else
            //    defectlist = _defectlist;

            ////画出defect
            //foreach (EmdefectlistEntity def in defectlist)
            //{
            //    if (string.IsNullOrEmpty(def.DieAddress))
            //        continue;

            //    if (items != null)
            //    {
            //        //显示定义的颜色
            //        var clr = items.FirstOrDefault(p => p.ITEMID == def.InspclassifiId);
            //        if (clr != null && string.IsNullOrEmpty(clr.USERID))
            //        {
            //            def.Color = clr.COLOR;
            //        }
            //    }

            //    ////die坐标信息集合
            //    //WR.Client.Controls.DefectCoordinate defectModel = new Controls.DefectCoordinate();

            //    //defectModel.Location = def.DieAddress;
            //    //defectModel.FillColor = def.Color;

            //    //picWafer.DefectList.Add(defectModel);
            //}

            ////picWafer.DefectList = defectlist.Select(s => new { Location = s.DieAddress, FillColor = s.Color }).Distinct()
            ////    .Select(s => new DefectCoordinate { Location = s.Location, FillColor = s.FillColor }).ToList();

            //var dlist = (from d in defectlist
            //             group d by d.DieAddress into g
            //             select new { Location = g.Key, Cclassid = g.Max(s => s.Cclassid), FillColor = g.Max(s => s.Color) })
            //             .Select(s =>
            //              new DefectCoordinate
            //              {
            //                  Location = s.Location,
            //                  FillColor = s.FillColor
            //              })
            //             .ToList();

            //picWafer.DefectList = dlist;
            //picWafer.DieLayoutList = listDieLayout;
            //picWafer.RowCnt = row;
            //picWafer.ColCnt = col;
            //picWafer.CurrentDefect = loction;
            //picWafer.HasDraw = true;

            ////picWafer.ReDraw(col, row, loction, listDieLayout, picWafer.DefectList);
            //picWafer.ReDraw();
        }

        /// <summary>
        /// 缺陷列表显示格式化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void grdData_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex > -1 && e.ColumnIndex > -1)
            {
                string col = grdData.Columns[e.ColumnIndex].Name;
                switch (col)
                {
                    case "ColImage":
                        if (imageList2.Images.Count < 1)
                            return;
                        string img = grdData["ColImageName", e.RowIndex].Value as string;
                        if (string.IsNullOrEmpty(img))
                            e.Value = imageList2.Images[1];
                        else
                            e.Value = imageList2.Images[0];
                        break;
                    case "ColCol":
                        string xy = grdData["ColDieAddress", e.RowIndex].Value.ToString();
                        var cr = xy.Split(new char[] { ',' });
                        e.Value = cr[0];
                        break;
                    case "ColRow":
                        string xy2 = grdData["ColDieAddress", e.RowIndex].Value.ToString();
                        var cr2 = xy2.Split(new char[] { ',' });
                        e.Value = cr2[1];
                        break;
                    //case "ColUpdated":
                    //    string ck = grdData["ColIschecked", e.RowIndex].Value.ToString();
                    //    e.Value = (ck == "1" ? "√" : "");
                    //    break;
                    //case "Colmanually":
                    //    string md = grdData["ColModifiedDefect", e.RowIndex].Value as string;
                    //    e.Value = (!string.IsNullOrEmpty(md) ? "√" : "");
                    //    break;
                    case "ColUpdated":
                        string md = grdData["ColModifiedDefect", e.RowIndex].Value as string;
                        e.Value = (!string.IsNullOrEmpty(md) ? "√" : "");
                        break;
                    case "Cclassid":
                        if (!tlsFinish.Enabled)
                        {
                            var answerValue = grdData["CorrectAnswer", e.RowIndex].Value;

                            if (!e.Value.Equals(answerValue))
                                e.CellStyle.ForeColor = Color.Red;
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        private void grdClass_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.Cancel = true;
            log.Error(e.Exception);
        }

        /// <summary>
        /// 显示class定义颜色
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void grdClass_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndex < 0)
                return;

            if (grdClass.Columns[e.ColumnIndex].DataPropertyName == "COLOR")
            {
                string color = grdClass[e.ColumnIndex, e.RowIndex].Value.ToString().ToUpper();
                e.Value = null;
                e.CellStyle.BackColor = ConvterColor(color);
            }
        }

        private Color ConvterColor(string color)
        {
            try
            {
                //return ColorTranslator.FromHtml(color);

                var newColor = Color.FromName(color);

                if (!newColor.IsKnownColor)
                {
                    if (!color.StartsWith("#"))
                        color = "#" + color;

                    if (color.Length > 7)
                        newColor = ColorTranslator.FromHtml(color.Substring(0, 7));
                    else
                        newColor = ColorTranslator.FromHtml(color);
                }

                return newColor;
            }
            catch
            {
                if (!color.StartsWith("#"))
                    color = "#" + color;

                if (color.Length > 7)
                    return ColorTranslator.FromHtml(color.Substring(0, 7));

                return ColorTranslator.FromHtml(color);
            }
        }

        /// <summary>
        /// 获取汇总后的points
        /// </summary>
        /// <returns></returns>
        private List<WmClassificationItemEntity> GetItemSum()
        {
            var lst = _defectlist;//grdData.DataSource as List<EmdefectlistEntity>;
            var cl = grdClass.DataSource as List<WMCLASSIFICATIONITEM>;

            if (cl == null)
                return new List<WmClassificationItemEntity>();

            if (lst == null)
                lst = new List<EmdefectlistEntity>();

            //汇总defect数
            var k = (from s in lst
                     group s by s.InspclassifiId into g
                     select new
                     {
                         g.Key,
                         cnt = g.Count()
                     }).ToList();

            var itmLst = (from c in cl
                          join t in k
                          on c.ITEMID equals t.Key
                          select new WmClassificationItemEntity
                          {
                              //DESCRIPTION = c.DESCRIPTION,
                              DESCRIPTION = c.NAME,
                              ID = c.ID,
                              InspectionType = "Front",
                              ITEMID = c.ITEMID,
                              Points = t.cnt,
                              SCHEMEID = c.SCHEMEID
                          }).ToList();
            //List<WmClassificationItemEntity> itmLst = new List<WmClassificationItemEntity>();

            //cl.ForEach((p) =>
            //{
            //    var itm = new WmClassificationItemEntity();
            //    itm.DESCRIPTION = p.NAME;
            //    itm.ID = p.ID;
            //    itm.InspectionType = "Front";
            //    itm.ITEMID = p.ITEMID;

            //    var ct = k.FirstOrDefault(a => a.Key == itm.ITEMID);
            //    itm.Points = ct == null ? 0 : ct.cnt;

            //    itm.SCHEMEID = p.SCHEMEID;
            //    itmLst.Add(itm);
            //});

            return itmLst;
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedTab == tabPage2)// && grdReport.DataSource == null)
            {
                //IwrService service = wrService.GetService();
                var list = GetItemSum();//service.GetClassSummary(Resultid);
                grdReport.DataSource = list;

                ChtShow(list);
            }
        }

        /// <summary>
        /// 显示图表
        /// </summary>
        /// <param name="list"></param>
        private void ChtShow(List<WmClassificationItemEntity> list)
        {
            var serie = chtDefect.Series[0];
            serie.Points.Clear();
            //serie.IsXValueIndexed = false;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Points < 1)
                    continue;

                DataPoint p = new DataPoint();

                //p.SetValueXY(i + 1, list[i].Points);
                var xValue = string.Empty;

                if (!string.IsNullOrEmpty(list[i].DESCRIPTION))
                    xValue = list[i].DESCRIPTION;
                p.SetValueXY(xValue, list[i].Points);
                p.Label = list[i].Points.ToString();

                serie.Points.Add(p);
            }
        }

        private void lblLotOut_Click(object sender, EventArgs e)
        {
            //picWafer.ZoomOut(10);
            picWafer.ZoomMultiple++;

            lblReclass.BorderStyle = BorderStyle.None;
        }

        private void lbl_P_In_MouseEnter(object sender, EventArgs e)
        {
            Label lbl = (Label)sender;
            lbl.BorderStyle = BorderStyle.FixedSingle;
        }

        private void lbl_P_In_MouseLeave(object sender, EventArgs e)
        {
            Label lbl = (Label)sender;
            lbl.BorderStyle = BorderStyle.None;
            lbl.Update();
        }

        private void lbl_P_In_Click(object sender, EventArgs e)
        {
            PicShow.ZoomOut(100);
            ResetTck();
        }

        private void lbl_P_Out_Click(object sender, EventArgs e)
        {
            PicShow.ZoomIn(100);
            ResetTck();
        }

        private void ResetTck()
        {
            tckContract.Value = 0;
            tckBright.Value = 0;
            if (lbl_P_Bright.BorderStyle == BorderStyle.Fixed3D)
            {
                tckContract.Visible = false;
                lbl_P_Bright.BorderStyle = BorderStyle.None;
            }

            if (lbl_P_BrightK.BorderStyle == BorderStyle.Fixed3D)
            {
                tckBright.Visible = false;
                lbl_P_BrightK.BorderStyle = BorderStyle.None;
            }
        }

        /// <summary>
        /// 灰度
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lbl_P_Bright_Click(object sender, EventArgs e)
        {
            if (tckBright.Visible)
            {
                tckBright.Value = 0;
                tckBright.Visible = false;
                lbl_P_BrightK.BorderStyle = BorderStyle.None;
            }

            //PicShow.MakeGray();
            //PicShow.BrightnessP(false);

            if (!tckContract.Visible)
            {
                tckContract.Visible = true;
                lbl_P_Bright.BorderStyle = BorderStyle.Fixed3D;
            }
            else
            {
                tckContract.Visible = false;
                lbl_P_Bright.BorderStyle = BorderStyle.None;
            }
        }

        /// <summary>
        /// 鼠标效果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lbl_PB_In_MouseEnter(object sender, EventArgs e)
        {
            if (tckContract.Visible)
                return;
            Label lbl = (Label)sender;
            lbl.BorderStyle = BorderStyle.FixedSingle;
        }

        private void lbl_PB_In_MouseLeave(object sender, EventArgs e)
        {
            if (tckContract.Visible)
                return;

            Label lbl = (Label)sender;
            lbl.BorderStyle = BorderStyle.None;
            lbl.Update();
        }

        /// <summary>
        /// 亮度
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lbl_P_BrightK_Click(object sender, EventArgs e)
        {
            if (tckContract.Visible)
            {
                tckContract.Value = 0;
                tckContract.Visible = false;
                lbl_P_Bright.BorderStyle = BorderStyle.None;
            }

            //PicShow.BrightnessP(true);
            if (!tckBright.Visible)
            {
                tckBright.Visible = true;
                lbl_P_BrightK.BorderStyle = BorderStyle.Fixed3D;
            }
            else
            {
                tckBright.Visible = false;
                lbl_P_BrightK.BorderStyle = BorderStyle.None;
            }
        }

        /// <summary>
        /// 鼠标效果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lbl_PK_In_MouseEnter(object sender, EventArgs e)
        {
            if (tckBright.Visible)
                return;
            Label lbl = (Label)sender;
            lbl.BorderStyle = BorderStyle.FixedSingle;
        }

        private void lbl_PK_In_MouseLeave(object sender, EventArgs e)
        {
            if (tckBright.Visible)
                return;

            Label lbl = (Label)sender;
            lbl.BorderStyle = BorderStyle.None;
            lbl.Update();
        }

        /// <summary>
        /// 复原图片
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lbl_P_Restore_Click(object sender, EventArgs e)
        {
            //PicShow.WrImage = PicShow.WrImage;
            if (PicShow.ZoomMultiple == 0)
                return;

            if (PicShow.ZoomMultiple > 0)
                PicShow.ZoomIn(PicShow.ZoomMultiple);
            else
                PicShow.ZoomOut(Math.Abs(PicShow.ZoomMultiple));

            PicShow.ZoomMultiple = 0;
        }

        /// <summary>
        /// 保存图片
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lbl_P_Save_Click(object sender, EventArgs e)
        {
            ResetTck();
            if (PicShow.WrImage == null)
                return;

            SaveFileDialog fd = new SaveFileDialog();
            fd.FileName = PicShow.Tag.ToString();
            if (DialogResult.OK == fd.ShowDialog())
            {
                PicShow.SaveImage(fd.FileName);
            }
        }

        private void lblLotIn_Click(object sender, EventArgs e)
        {
            //picWafer.ZoomIn(10);
            picWafer.ZoomMultiple--;
            lblReclass.BorderStyle = BorderStyle.None;
        }

        private void mnFront_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (e.ClickedItem == tlsSave)
            {
                if (SaveHotKey())
                {
                    SetClsMenu();
                }
            }
            else if (e.ClickedItem == tlsClassCancel)
            {
                SetClsMenu();

                List<WMCLASSIFICATIONITEM> items = grdClass.DataSource as List<WMCLASSIFICATIONITEM>;
                if (items == null || items.Count < 1)
                    return;

                items.ForEach((p) =>
                {
                    if (!string.IsNullOrEmpty(p.USERID))
                    {
                        string[] r = p.USERID.Split(new char[] { '|' });
                        p.HOTKEY = r[0];
                        p.COLOR = r[1];
                        p.USERID = "";
                    }
                });

                grdClass.Invalidate();
            }
            else
            {
                //colHotKey.ReadOnly = false;
                grdClass.Columns["colHotKey"].ReadOnly = false;

                tlsEdit.Enabled = false;
                tlsEdit.Checked = true;

                tlsSave.Enabled = true;
                tlsClassCancel.Enabled = true;
            }
        }

        private void SetClsMenu()
        {
            tlsEdit.Enabled = true;
            tlsEdit.Checked = false;

            tlsSave.Enabled = false;
            tlsClassCancel.Enabled = false;

            //colHotKey.ReadOnly = true;
            grdClass.Columns["colHotKey"].ReadOnly = true;
        }

        /// <summary>
        /// 保存自定义缺陷
        /// </summary>
        /// <returns></returns>
        private bool SaveHotKey()
        {
            grdClass.EndEdit();

            List<WMCLASSIFICATIONITEM> items = grdClass.DataSource as List<WMCLASSIFICATIONITEM>;
            if (items == null || items.Count < 1)
                return false;

            StringBuilder sbt = new StringBuilder();
            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(item.USERID))
                {
                    if (items.Any(p => p.ITEMID != item.ITEMID && p.HOTKEY == item.HOTKEY && !string.IsNullOrEmpty(item.HOTKEY)))
                    {
                        MsgBoxEx.Info(string.Format("Acc Keys[{0}] already repeated!", DataCache.CmnDict.FirstOrDefault(p => p.DICTID == "2010" && p.CODE == item.HOTKEY).NAME));
                        return false;
                    }

                    sbt.AppendFormat(";{0}|{1}|{2}", item.ITEMID, item.HOTKEY, item.COLOR);
                }
            }

            if (sbt.Length < 1)
                return true;

            IwrService service = wrService.GetService();
            int res = service.UpdateClassificationItemUser(sbt.ToString(), DataCache.UserInfo.ID);
            if (res == 1)
            {
                items.ForEach((p) => { p.USERID = ""; });
            }

            if (grdData.SelectedRows == null || grdData.SelectedRows.Count < 1)
            {
                DrawDefect("0,0");
                return true;
            }

            var ent = grdData.SelectedRows[0].DataBoundItem as EmdefectlistEntity;
            DrawDefect(ent.DieAddress);

            return true;
        }

        private void grdClass_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex > -1 && e.RowIndex > -1)
            {
                if (grdClass.Columns[e.ColumnIndex].Name == "Column4")
                {
                    if (tlsEdit.Enabled)
                        return;

                    if (clrDialog.ShowDialog() == DialogResult.OK)
                    {
                        //grdClass[e.ColumnIndex, e.RowIndex].Style.BackColor = clrDialog.Color;
                        var ent = grdClass.Rows[e.RowIndex].DataBoundItem as WMCLASSIFICATIONITEM;
                        SetItem(ent);
                        ent.COLOR = ColorTranslator.ToHtml(clrDialog.Color);
                        grdClass.Invalidate();
                        grdClass.ClearSelection();
                    }
                }
            }
        }

        private void grdData_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {

        }

        /// <summary>
        /// 列表显示
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tlsList_Click(object sender, EventArgs e)
        {
            if (!grdData.Visible)
            {
                var list = grdData.DataSource as List<EmdefectlistEntity>;
                grdData.Visible = true;
                //grdData.DataSource = new BindingCollection<EmdefectlistEntity>(list);
                grdData.DataSource = list;

                lstView.Visible = false;

                if (lstView.SelectedIndices != null && lstView.SelectedIndices.Count > 0)
                {
                    if (list == null)
                        return;

                    int? id = list[lstView.SelectedIndices[0]].Id;
                    foreach (DataGridViewRow row in grdData.Rows)
                    {
                        EmdefectlistEntity ent = row.DataBoundItem as EmdefectlistEntity;
                        if (ent != null && ent.Id == id)
                        {
                            row.Selected = true;
                            grdData.CurrentCell = row.Cells[grdData.CurrentCell.ColumnIndex];
                            if (!row.Displayed)
                                grdData.FirstDisplayedScrollingRowIndex = row.Index;

                            break;
                        }
                    }
                }

                grdData.Focus();
            }
        }

        /// <summary>
        /// 缩略图显示
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tlsThum_Click(object sender, EventArgs e)
        {
            if (!lstView.Visible)
            {
                grdData.Visible = false;
                lstView.Visible = true;
                lstView.Focus();

                if (grdData.SelectedRows != null && grdData.SelectedRows.Count > 0)
                {
                    EmdefectlistEntity selectedent = grdData.SelectedRows[0].DataBoundItem as EmdefectlistEntity;
                    List<EmdefectlistEntity> data = grdData.DataSource as List<EmdefectlistEntity>;
                    int idx = data.FindIndex(p => p.Id == selectedent.Id);
                    lstView.Items[idx].EnsureVisible();
                    lstView.Items[idx].Selected = true;
                    lstView.Items[idx].Focused = true;
                }
            }
        }

        /// <summary>
        /// 列表显示defect
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lstView_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            var list = grdData.DataSource as List<EmdefectlistEntity>;
            if (list == null)
                return;

            var ent = list[e.ItemIndex];
            e.Item = new ListViewItem(string.Format("ID:{0} Class:{1}", list[e.ItemIndex].Id, list[e.ItemIndex].Cclassid));
            e.Item.BackColor = SystemColors.InactiveCaption;

            if (string.IsNullOrEmpty(ent.ImageName))
                return;

            try
            {
                if (!imgsView.Images.ContainsKey(ent.ImageName))
                {
                    IwrService service = wrService.GetService();
                    //Stream st = service.GetPic(Resultid + "\\" + ent.ImageName);
                    Stream st = service.GetPic(ent.ImageName);
                    Image pic = Image.FromStream(st, true);
                    imgsView.Images.Add(ent.ImageName, pic);
                }
                e.Item.ImageIndex = imgsView.Images.IndexOfKey(ent.ImageName);
            }
            catch { }
        }

        /// <summary>
        /// 切换图片
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lstView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstView.SelectedIndices != null && lstView.SelectedIndices.Count > 0)
            {
                ResetTck();
                var list = grdData.DataSource as List<EmdefectlistEntity>;
                if (list == null)
                    return;
                var ent = list[lstView.SelectedIndices[0]];
                string name = ent.ImageName;
                GetImage(name);
                DrawDefect(ent.DieAddress);
            }
        }

        /// <summary>
        /// 上一条wafer记录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tlsPsample_Click(object sender, EventArgs e)
        {
            if (IsSave == false && MsgBoxEx.ConfirmYesNo("Are you sure to save the changes") == DialogResult.Yes)
                timer2_Tick(sender, e);

            int total = DataCache.WaferResultInfo.Count;
            if (total <= 1)
                return;

            if (tlsFilter.Checked)
                tlsFilter.Checked = false;

            int nextid = 0;
            int currid = DataCache.WaferResultInfo.FindIndex(p => p.RESULTID == Resultid);
            if (currid < 0)
                nextid = 0;
            else if (currid == 0)
                nextid = total - 1;
            else
                nextid = currid - 1;

            var ent = DataCache.WaferResultInfo[nextid];
            Resultid = ent.RESULTID;
            SaveResultid(Resultid, ent.LOT, ent.SUBSTRATE_ID, ent.NUMDEFECT, ent.SFIELD);

            Oparams = new string[] { ent.RESULTID, ent.LOT, ent.SUBSTRATE_ID, ent.NUMDEFECT.ToString(), ent.SFIELD.ToString() };
            lblWaferID.Text = string.Format("Lot:{0}  Wafer:{1} Defect Die:{2} Yield:{3}", ent.LOT, ent.SUBSTRATE_ID, ent.NUMDEFECT, ent.SFIELD);

            hasDraw = true;
            IsSave = true;
            picWafer.ZoomMultiple = 1;
            InitData();

            SetClsMenu();

            if (grdData.Visible)
                this.ActiveControl = grdData;
            else
                this.ActiveControl = lstView;
        }

        private void SaveResultid(string id, string lot, string substrate, long? defectCnt, decimal? yield)
        {
            frm_main frm = this.Tag as frm_main;
            if (frm != null)
                frm.Oparams = new string[] { id, lot, substrate, defectCnt.ToString(), yield.ToString() };
        }

        /// <summary>
        /// 下一条wafer记录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tlsNsample_Click(object sender, EventArgs e)
        {
            if (IsSave == false && MsgBoxEx.ConfirmYesNo("Are you sure to save the changes") == DialogResult.Yes)
                timer2_Tick(sender, e);

            int total = DataCache.WaferResultInfo.Count;
            if (total <= 1)
                return;

            if (tlsFilter.Checked)
                tlsFilter.Checked = false;

            int nextid = 0;
            int currid = DataCache.WaferResultInfo.FindIndex(p => p.RESULTID == Resultid);
            if (currid < 0)
                nextid = 0;
            else if (currid == total - 1)
                nextid = 0;
            else
                nextid = currid + 1;

            var ent = DataCache.WaferResultInfo[nextid];
            Resultid = ent.RESULTID;
            SaveResultid(Resultid, ent.LOT, ent.SUBSTRATE_ID, ent.NUMDEFECT, ent.SFIELD);

            Oparams = new string[] { ent.RESULTID, ent.LOT, ent.SUBSTRATE_ID, ent.NUMDEFECT.ToString(), ent.SFIELD.ToString() };
            lblWaferID.Text = string.Format("Lot:{0}  Wafer:{1} Defect Die:{2} Yield:{3}", ent.LOT, ent.SUBSTRATE_ID, ent.NUMDEFECT, ent.SFIELD);

            hasDraw = true;
            IsSave = true;

            picWafer.ZoomMultiple = 1;
            InitData();

            SetClsMenu();

            if (grdData.Visible)
                this.ActiveControl = grdData;
            else
                this.ActiveControl = lstView;
        }

        /// <summary>
        /// 过滤有图片的记录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tlsFilter_Click(object sender, EventArgs e)
        {
            if (!tlsFilter.Checked)
            {
                tlsFilter.Checked = true;
                var tlst = _defectlist.Where(p => !string.IsNullOrEmpty(p.ImageName)).ToList();

                //grdData.DataSource = new BindingCollection<EmdefectlistEntity>(tlst);
                grdData.DataSource = tlst;
                lstView.VirtualListSize = tlst.Count;

                if (grdData.Visible)
                {
                    grdData.Refresh();
                    if (tlst.Count > 0)
                    {
                        grdData.Rows[0].Selected = true;
                        grdData.CurrentCell = grdData[0, 0];
                    }
                }
                else if (lstView.Visible)
                {
                    lstView.Refresh();
                    if (tlst.Count > 0)
                    {
                        lstView.Items[0].Selected = true;
                        lstView.Items[0].Focused = true;
                        lstView.EnsureVisible(0);

                        DrawDefect(tlst[0].DieAddress);
                    }
                }
            }
            else
            {
                tlsFilter.Checked = false;
                if (grdData.Visible)
                {
                    grdData.Refresh();
                    if (_defectlist.Count > 0)
                    {
                        // grdData.CurrentCell = grdData[0, 0];
                        // grdData.Rows[0].Selected = true;
                    }
                }
                else if (lstView.Visible)
                {
                    lstView.Refresh();
                    if (_defectlist.Count > 0)
                    {
                        lstView.Items[0].Selected = true;
                        lstView.Items[0].Focused = true;
                        lstView.EnsureVisible(0);

                        DrawDefect(_defectlist[0].DieAddress);
                    }
                }

                //grdData.DataSource = new BindingCollection<EmdefectlistEntity>(_defectlist);
                grdData.DataSource = _defectlist;
                lstView.VirtualListSize = _defectlist.Count;
            }
        }

        /// <summary>
        /// 上一条defect
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tlsPpoint_Click(object sender, EventArgs e)
        {
            if (grdData.Rows.Count < 2)
                return;

            int rowidx;

            if (grdData.Visible)
            {
                if (grdData.SelectedRows == null || grdData.SelectedRows.Count < 1)
                    rowidx = 0;
                else
                {
                    rowidx = grdData.SelectedRows[0].Index;
                    if (rowidx == 0)
                        rowidx = grdData.Rows.Count - 1;
                    else
                        rowidx = rowidx - 1;
                }

                grdData.Rows[rowidx].Selected = true;
                grdData.CurrentCell = grdData.Rows[rowidx].Cells[grdData.CurrentCell.ColumnIndex];

                if (!grdData.Rows[rowidx].Displayed)
                    grdData.FirstDisplayedScrollingRowIndex = rowidx;
            }
            else
            {
                if (!lstView.Focused)
                    lstView.Focus();

                if (lstView.SelectedIndices == null || lstView.SelectedIndices.Count < 1)
                    rowidx = 0;
                else
                {
                    rowidx = lstView.SelectedIndices[0];
                    if (rowidx == 0)
                        rowidx = grdData.Rows.Count - 1;
                    else
                        rowidx = rowidx - 1;
                }

                lstView.Items[rowidx].Selected = true;
                lstView.Items[rowidx].Focused = true;
                lstView.EnsureVisible(rowidx);
            }
        }

        /// <summary>
        /// 下一条defect
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tlsNpoint_Click(object sender, EventArgs e)
        {
            if (grdData.Rows.Count < 2)
                return;

            int rowidx;

            if (grdData.Visible)
            {
                if (grdData.SelectedRows == null || grdData.SelectedRows.Count < 1)
                    rowidx = 0;
                else
                {
                    rowidx = grdData.SelectedRows[0].Index;
                    if (rowidx == grdData.Rows.Count - 1)
                        rowidx = 0;
                    else
                        rowidx = rowidx + 1;
                }

                grdData.Rows[rowidx].Selected = true;
                grdData.CurrentCell = grdData.Rows[rowidx].Cells[grdData.CurrentCell.ColumnIndex];

                if (!grdData.Rows[rowidx].Displayed)
                    grdData.FirstDisplayedScrollingRowIndex = rowidx;
            }
            else
            {
                if (!lstView.Focused)
                    lstView.Focus();

                if (lstView.SelectedIndices == null || lstView.SelectedIndices.Count < 1)
                    rowidx = 0;
                else
                {
                    rowidx = lstView.SelectedIndices[0];
                    if (rowidx == grdData.Rows.Count - 1)
                        rowidx = 0;
                    else
                        rowidx = rowidx + 1;
                }
                lstView.Items[rowidx].Selected = true;
                lstView.Items[rowidx].Focused = true;
                lstView.EnsureVisible(rowidx);
            }
        }

        /// <summary>
        /// 获取修改的defect
        /// 格式：id,passid,inspid,inspclassifiid;id,passid,inspid,inspclassifiid
        /// </summary>
        /// <returns></returns>
        private string GetModifyDefect()
        {
            //var defs = grdData.DataSource as List<EmdefectlistEntity>;
            var defs = _defectlist;
            if (defs == null)
                return "";

            //修改后的defect
            var ms = defs.Where(p => !string.IsNullOrEmpty(p.ModifiedDefect));
            if (ms == null || ms.Count() < 1)
                return "";

            StringBuilder sbt = new StringBuilder();
            foreach (var item in ms)
            {
                var array = item.DieAddress.Split(',');
                sbt.AppendFormat(";{0},{1},{2},{3},{4},{5},{6}", item.Id, item.PASSID, item.INSPID, item.InspclassifiId, array[0], array[1], item.Cclassid);
            }

            sbt.Remove(0, 1);
            return sbt.ToString();
        }

        /// <summary>
        /// 保存结果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tlsSaveResult_Click(object sender, EventArgs e)
        {
            if (GetDataArchiveStatus())
            {
                MsgBoxEx.Info("Data is archiving, please wait a moment");
                return;
            }

            ShowLoading(ToopEnum.saving);

            IwrService service = wrService.GetService();
            int res = service.UpdateExamResult(Resultid, DataCache.UserInfo.ID, GetModifyDefect(), "1");
            if (res >= 0)
            {
                //var ent = service.GetWaferResultById(Resultid);
                //var wf = DataCache.WaferResultInfo.FirstOrDefault(p => p.RESULTID == Resultid);
                //wf.ISCHECKED = ent.ISCHECKED;
                //wf.CHECKEDDATE = ent.CHECKEDDATE;
                //wf.NUMDEFECT = ent.NUMDEFECT;
                //wf.SFIELD = ent.SFIELD;


                //更新坐标图
                if (grdData.Visible && grdData.SelectedRows != null && grdData.SelectedRows.Count > 0)
                {
                    var dent = grdData.SelectedRows[0].DataBoundItem as EmdefectlistEntity;
                    DrawDefect(dent.DieAddress);
                }
                else if (lstView.Visible && lstView.SelectedIndices != null && lstView.SelectedIndices.Count > 0)
                {
                    var list = grdData.DataSource as List<EmdefectlistEntity>;
                    DrawDefect(list[lstView.SelectedIndices[0]].DieAddress);
                }

                IsSave = true;
            }

            CloseLoading();
        }

        /// <summary>
        /// 完成检查
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tlsFinish_Click(object sender, EventArgs e)
        {
            if (GetDataArchiveStatus())
            {
                MsgBoxEx.Info("Data is archiving, please wait a moment");
                return;
            }

            if (MsgBoxEx.ConfirmYesNo(MessageConst.frm_preview_msg001) == DialogResult.No)
                return;

            ShowLoading(ToopEnum.saving);

            try
            {
                IwrService service = wrService.GetService();
                int res = service.UpdateExamResult(Resultid, DataCache.UserInfo.ID, GetModifyDefect(), "2");

                if (res >= 0)
                {
                    tlsSaveResult.Enabled = false;
                    tlsFinish.Enabled = false;
                    tlsReclass.Enabled = false;

                    //var ent = service.GetWaferResultById(Resultid);
                    //var wf = DataCache.WaferResultInfo.FirstOrDefault(p => p.RESULTID == Resultid);
                    //wf.ISCHECKED = ent.ISCHECKED;
                    //wf.CHECKEDDATE = ent.CHECKEDDATE;
                    //wf.NUMDEFECT = ent.NUMDEFECT;
                    //wf.SFIELD = ent.SFIELD;

                    //if (wf.ISCHECKED == "2")
                    //{
                    //    tlsSaveResult.Enabled = false;
                    //    tlsFinish.Enabled = false;
                    //    tlsReclass.Enabled = false;
                    //}
                    //else
                    //{
                    //    tlsSaveResult.Enabled = true;
                    //    tlsFinish.Enabled = true;
                    //    tlsReclass.Enabled = true;
                    //}

                    if (grdData.SelectedRows != null && grdData.SelectedRows.Count > 0)
                    {
                        var dent = grdData.SelectedRows[0].DataBoundItem as EmdefectlistEntity;
                        DrawDefect(dent.DieAddress);
                    }

                    IsSave = true;
                }
            }
            finally
            {
                CloseLoading();
            }
        }

        /// <summary>
        /// 右键class
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void grdData_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex > -1 && e.ColumnIndex > -1 && e.Button == MouseButtons.Right)
            {
                grdData.CurrentCell = grdData[e.ColumnIndex, e.RowIndex];
                cnmReclass.Show(MousePosition.X, MousePosition.Y);

                cnmReclass.Tag = "1";
            }
        }

        /// <summary>
        /// 画定位线
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void picWafer_Paint(object sender, PaintEventArgs e)
        {
            if (string.IsNullOrEmpty(picWafer.Status))
            {
                lblReclass.BorderStyle = BorderStyle.None;
            }

            //if (_dieWidth < 0 || _dieHeight < 0)
            //    return;

            //if (grdData.SelectedRows == null || grdData.SelectedRows.Count < 1)
            //    return;

            //var ent = grdData.SelectedRows[0].DataBoundItem as EmdefectlistEntity;
            //if (string.IsNullOrEmpty(ent.DieAddress))
            //    return;

            //string[] addr = ent.DieAddress.Split(new char[] { ',' });

            ////横线
            //e.Graphics.DrawLine(_linePen, 0, int.Parse(addr[1]) * _dieHeight + 1 + _offsetY, picWafer.Width, int.Parse(addr[1]) * _dieHeight + 1 + _offsetY);
            ////竖线
            //e.Graphics.DrawLine(_linePen, int.Parse(addr[0]) * _dieWidth + 1 + _offsetX, 0, int.Parse(addr[0]) * _dieWidth + 1 + _offsetX, picWafer.Height);
        }

        private void picWafer_DoubleClick(object sender, EventArgs e)
        {
            if (picWafer.WrImage == null)
                return;

            frm_zoom frm = new frm_zoom();
            frm.picBox.BackgroundImage = picWafer.WrImage;
            frm.Text = lblWaferID.Text;
            frm.ShowDialog();
        }

        /// <summary>
        /// 操作快捷键
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="keyData"></param>
        /// <returns></returns>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (tlsFinish.Enabled)
            {
                string key = keyData.ToString().Replace(" ", "").Replace("D", "").Replace("NumPad", "");
                var ky = DataCache.CmnDict.FirstOrDefault(p => p.DICTID == "2010" && key == p.VALUE.Trim());
                if (ky != null)
                {
                    List<WMCLASSIFICATIONITEM> cls = grdClass.DataSource as List<WMCLASSIFICATIONITEM>;
                    if (cls != null)
                    {
                        var clf = cls.FirstOrDefault(p => p.HOTKEY == ky.CODE && string.IsNullOrEmpty(p.USERID));
                        if (clf != null)
                        {
                            if (grdData.Visible)
                            {
                                if (grdData.SelectedRows != null && grdData.SelectedRows.Count > 0)
                                {
                                    var ent = grdData.SelectedRows[0].DataBoundItem as EmdefectlistEntity;
                                    if (ent != null)
                                    {
                                        ent.Cclassid = clf.ID;
                                        ent.InspclassifiId = clf.ITEMID;
                                        ent.ModifiedDefect = ent.INSPID;
                                        ent.Description = clf.NAME;
                                        grdData.InvalidateRow(grdData.SelectedRows[0].Index);

                                        UpdateDefectClassification(ent);

                                        //DrawDefect(ent.DieAddress);

                                        tabControl1_SelectedIndexChanged(null, null);
                                    }
                                }
                            }
                            else
                            {
                                if (lstView.SelectedIndices != null && lstView.SelectedIndices.Count > 0)
                                {
                                    List<EmdefectlistEntity> list = grdData.DataSource as List<EmdefectlistEntity>;
                                    var ent = list[lstView.SelectedIndices[0]];
                                    ent.Cclassid = clf.ID;
                                    ent.InspclassifiId = clf.ITEMID;
                                    ent.ModifiedDefect = ent.INSPID;
                                    ent.Description = clf.NAME;

                                    UpdateDefectClassification(ent);

                                    lstView.RedrawItems(lstView.SelectedIndices[0], lstView.SelectedIndices[0], false);
                                    DrawDefect(ent.DieAddress);

                                    tabControl1_SelectedIndexChanged(null, null);
                                }
                            }
                        }
                    }
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// 窗体打开后定位控件，以备使用快捷键
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer1_Tick(object sender, EventArgs e)
        {
            grdData.Focus();
            timer1.Enabled = false;
            timer2.Enabled = true;
        }

        private void grdClass_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            var item = grdClass.Rows[e.RowIndex].DataBoundItem as WMCLASSIFICATIONITEM;
            SetItem(item);
        }

        private void SetItem(WMCLASSIFICATIONITEM item)
        {
            if (item == null)
                return;

            //已经保存
            if (!string.IsNullOrEmpty(item.USERID))
                return;

            item.USERID = string.Format("{0}|{1}", item.HOTKEY, item.COLOR);
        }

        private void tckBright_Scroll(object sender, EventArgs e)
        {
            PicShow.BrightnessP(tckBright.Value);
        }

        private void tckContract_Scroll(object sender, EventArgs e)
        {
            PicShow.MkBrightness(tckContract.Value);
        }

        private void PicShow_Click(object sender, EventArgs e)
        {
            ResetTck();
        }

        private void panel5_Click(object sender, EventArgs e)
        {
            ResetTck();
        }

        /// <summary>
        /// 
        /// </summary>
        private void UpdateDefectClassification(EmdefectlistEntity model)
        {
            var count = grdData.CurrentCell.RowIndex;
            IsSave = false;

            List<EmdefectlistEntity> list = grdData.DataSource as List<EmdefectlistEntity>;

            if ((cnmReclass.Tag != null && cnmReclass.Tag.ToString() == "2") || model.Cclassid != 0)
            {
                //获取同一个image下其他的缺陷
                if (!string.IsNullOrEmpty(model.ImageName))
                {
                    var defectIdList = list.Where(s => s.ImageName == model.ImageName
                          && s.Id != model.Id)
                          .Select(s => s.Id).ToList();

                    foreach (var id in defectIdList)
                    {
                        var index = list.FindIndex(s => s.Id == id);

                        list[index].Cclassid = model.Cclassid;
                        list[index].InspclassifiId = model.InspclassifiId;
                        list[index].ModifiedDefect = model.ModifiedDefect;
                        list[index].Description = model.Description;

                        //UpdateDieLayout(list[index].DieAddress, (int)model.Cclassid);

                        if (grdData.Visible)
                            grdData.InvalidateRow(index);

                        count = index;
                    }

                    //count = (int)model.Id;
                    if (grdData.CurrentCell.RowIndex > count)
                        count = grdData.CurrentCell.RowIndex;
                }
            }

            if (cnmReclass.Tag != null && cnmReclass.Tag.ToString() == "2")
            {
                //获取die下其他的缺陷
                var defectIdList = list.Where(s => s.DieAddress == model.DieAddress
                    && s.PASSID == model.PASSID && s.INSPID == model.INSPID && s.Id != model.Id)
                    .Select(s => s.Id).ToList();

                //count += defectIdList.Count;

                foreach (var id in defectIdList)
                {
                    var index = list.FindIndex(s => s.Id == id);

                    list[index].Cclassid = model.Cclassid;
                    list[index].InspclassifiId = model.InspclassifiId;
                    list[index].ModifiedDefect = model.ModifiedDefect;
                    list[index].Description = model.Description;

                    count = index;
                    if (grdData.Visible)
                        grdData.InvalidateRow(index);
                }

                if (model.Id > count)
                    count = (int)model.Id;
            }

            //UpdateDieLayout(model.DieAddress, (int)model.Cclassid);

            if (cnmReclass.Tag == null || cnmReclass.Tag.ToString() != "2")
            {
                InitClassList();

                //if (grdData.Rows.Count > grdData.CurrentCell.RowIndex + count)
                //grdData.CurrentCell = grdData[grdData.CurrentCell.ColumnIndex, grdData.CurrentCell.RowIndex + count];
                if (grdData.Rows.Count > count + 1)
                    grdData.CurrentCell = grdData[grdData.CurrentCell.ColumnIndex, count + 1];
                else
                    grdData.CurrentCell = grdData[grdData.CurrentCell.ColumnIndex, grdData.Rows.Count - 1];

            }
            //重新计算良率 
            //decimal goodCnt = _dielayoutlist.Count(s => s.INSPCLASSIFIID == 0);
            //decimal defectCnt = _dielayoutlist.Count(s => s.INSPCLASSIFIID != 0);

            //Oparams[3] = defectCnt.ToString();
            //Oparams[4] = (goodCnt / _dielayoutlist.Count * 100).ToString("0.00");

            //lblWaferID.Text = string.Format("Lot:{0}  Wafer:{1} Defect Die:{2} Yield:{3}", Oparams[1], Oparams[2], Oparams[3], Oparams[4]);
        }

        /// <summary>
        /// 更新layout缺陷类型
        /// </summary>
        /// <param name="dieAddress"></param>
        /// <param name="cclassid"></param>
        private void UpdateDieLayout(string dieAddress, int cclassid)
        {
            var array = dieAddress.Split(',');

            int x = int.Parse(array[0]);
            int y = int.Parse(array[1]);
            var index = _dielayoutlist.FindIndex(s => s.DIEADDRESSX == x && s.DIEADDRESSY == y);

            if (index != -1)
                _dielayoutlist[index].INSPCLASSIFIID = cclassid;
        }

        /// <summary>
        /// 缺陷改变
        /// </summary>
        /// <param name="e"></param>
        private void picWafer_DefectChanged(Controls.EventDefectArg e)
        {
            try
            {
                var selectDefectList = _defectlist.Where(s => picWafer.SelectDefect.Contains(s.DieAddress)).ToList();

                //grdData.DataSource = new BindingCollection<EmdefectlistEntity>(selectDefectList);
                grdData.DataSource = selectDefectList;

                lstView.VirtualListSize = selectDefectList.Count;

                DrawDefect(e.Location);
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 复位
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lblReset_Click(object sender, EventArgs e)
        {
            //if (picWafer.ZoomMultiple == 1)
            //    return;

            //if (picWafer.ZoomMultiple > 0)
            //    picWafer.ZoomIn(picWafer.ZoomMultiple);
            //else
            //    picWafer.ZoomOut(Math.Abs(picWafer.ZoomMultiple));

            picWafer.ZoomMultiple = 1;
            lblReclass.BorderStyle = BorderStyle.None;
        }

        /// <summary>
        /// 定时保存
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer2_Tick(object sender, EventArgs e)
        {
            if (tlsSaveResult.Enabled == true)
            {
                if (GetDataArchiveStatus())
                    return;

                ShowLoading(ToopEnum.saving);

                try
                {
                    //log.Debug("SaveResult Start...............");
                    IwrService service = wrService.GetService();

                    //log.Debug("UpdateExamResult Start...............");
                    int res = service.UpdateExamResult(Resultid, DataCache.UserInfo.ID, GetModifyDefect(), "1");
                    //log.Debug("UpdateExamResult End...............");
                    if (res >= 0)
                    {
                        IsSave = true;
                    }

                    //log.Debug("SaveResult End...............");
                }
                finally
                {
                    CloseLoading();
                }
            }
        }

        /// <summary>
        /// screen capture
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lblCapture_Click(object sender, EventArgs e)
        {
            FrmCapture frmCapture = new FrmCapture();
            frmCapture.IsCaptureCursor = true;
            frmCapture.Show();
        }

        private void tlsClass_SelectedIndexChanged(object sender, EventArgs e)
        {
            var classId = -1;

            if (tlsClass.ComboBox.SelectedValue != null)
                classId = Convert.ToInt32(tlsClass.ComboBox.SelectedValue);

            var list = _defectlist;

            if (classId != -1)
                list = _defectlist.Where(s => s.Cclassid == Convert.ToInt32(classId)).ToList();

            //grdData.DataSource = new BindingCollection<EmdefectlistEntity>(list);
            grdData.DataSource = list;

            if (list.Count > 0)
                DrawDefect(list[0].DieAddress);
        }

        //private void tlsNewClass_SelectedIndexChanged(object sender, EventArgs e)
        //{
        //    var select = tlsNewClass.SelectedItem as ObjectSelectionWrapper<ClassDropDownModel>;
        //    var classArray = select.NameConcatenated;

        //    var list = _defectlist;

        //    if (!string.IsNullOrEmpty(classArray))
        //        list = _defectlist.Where(s => classArray.Contains(s.Description)).ToList();

        //    //grdData.DataSource = new BindingCollection<EmdefectlistEntity>(list);
        //    grdData.DataSource = list;

        //    if (list.Count > 0)
        //        DrawDefect(list[0].DieAddress);
        //}

        private void tlsNewClass_CheckBoxCheckedChanged(object sender, EventArgs e)
        {
            var list = _defectlist;

            if (!string.IsNullOrEmpty(tlsNewClass.Text))
                list = _defectlist.Where(s => tlsNewClass.Text.Contains(s.Description)).ToList();

            grdData.DataSource = list;

            if (list.Count > 0)
                DrawDefect(list[0].DieAddress);
        }

        /// <summary>
        /// 保存界面布局
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void frm_preview_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (IsSave == false && MsgBoxEx.ConfirmYesNo("Are you sure to save the changes") == DialogResult.Yes)
                timer2_Tick(sender, e);

            //if (IsLayoutRole)
            //{
            //    var layout = string.Empty;

            //    foreach (var item in this.Controls)
            //    {
            //        if (item is Panel)
            //        {
            //            var control = item as Panel;

            //            layout += string.Format("{0}:{1},{2};", control.Name, control.Width, control.Height);
            //        }
            //    }

            //    layout += string.Format("{0}:{1},{2};", panel4.Name, panel4.Width, panel4.Height);
            //    layout += string.Format("{0}:{1},{2};", tabControl1.Name, tabControl1.Width, tabControl1.Height);

            //    System.Configuration.Configuration config = WR.Utils.Config.GetConfig();
            //    config.AppSettings.Settings.Remove("previewLayout");
            //    config.AppSettings.Settings.Add("previewLayout", layout);

            //    config.Save();

            //    WR.Utils.Config.Refresh();
            //}
        }

        private void splitter1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            //panel4.Height = Convert.ToInt32(panel4.Width * 0.8);
            var height = Convert.ToInt32(panel4.Width * 0.8);

            tabControl1.Height = panel2.Height - height;

            DrawDefect(picWafer.CurrentDefect);
        }

        private void splitter2_SplitterMoved(object sender, SplitterEventArgs e)
        {
            panel2.Width = Convert.ToInt32(panel4.Height * 1.25);

            DrawDefect(picWafer.CurrentDefect);
        }

        private void picWafer_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && picWafer.SelectRect != null && picWafer.Status == "Reclass" && picWafer.SelectDefect.Count > 0)
            {
                if (picWafer.SelectRect.Contains(e.X, e.Y))
                {
                    cnmReclass.Show(MousePosition.X, MousePosition.Y);
                    cnmReclass.Tag = "2";
                }
            }
        }

        /// <summary>
        /// 复判
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lblReclass_Click(object sender, EventArgs e)
        {
            if (lblReclass.BorderStyle == BorderStyle.None)
            {
                picWafer.Status = "Reclass";
                lblReclass.BorderStyle = BorderStyle.Fixed3D;
            }
            else
            {
                picWafer.Status = "";
                lblReclass.BorderStyle = BorderStyle.None;
            }
        }

        private void timer3_Tick(object sender, EventArgs e)
        {
            timer3.Enabled = false;

            DrawDefect(dieLoction);
        }

        private void frm_preview_Shown(object sender, EventArgs e)
        {
            //GetLayout();
            //panel2.Width = Convert.ToInt32(panel4.Height * 1.25);
        }

        private bool GetDataArchiveStatus()
        {
            IsysService service = sysService.GetService();

            return service.GetCmn("3020").Count(s => s.CODE == "1") > 0;
        }

        private void timer4_Tick(object sender, EventArgs e)
        {
            if (examResult != null)
            {
                var span = (examResult.PLANENDDATE - DateTime.Now);

                var seconds = span.TotalSeconds;

                if (seconds <= 0)
                {
                    timer4.Enabled = false;

                    tlsFinish_Click(sender, e);

                    lblWaferID.Text = "Remaining Time:00:00:00";
                }
                else
                    lblWaferID.Text = string.Format("Remaining Time:{0}", span.ToString(@"hh\:mm\:ss"));
            }
        }
    }
}
