﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.UI;
using System.Data;
using System.Linq;
using UnityEngine.Events;
using System;

namespace ExpMngr
{
    public class ParticipantListSelection : MonoBehaviour
    {

        public string currentFolder;
        string ppListPath;
        public DataTable ppList = null;

        public Text ppListNameDisplay;
        public ParticipantSelector participantSelector;
        public ExperimentSession experiment;
        public ExperimentStartupController startup;
        public FillableFormController form;
        public PopupController popupController;
        public Button startButton;

        public void Init()
        {
            ExperimentStartupController.SetSelectableAndChildrenInteractable(participantSelector.gameObject, false);
            ExperimentStartupController.SetSelectableAndChildrenInteractable(form.gameObject, false);
            ExperimentStartupController.SetSelectableAndChildrenInteractable(startButton.gameObject, false);
        }


        public void SelectList()
        {
            SFB.StandaloneFileBrowser.OpenFilePanelAsync("Select participant list", currentFolder, "csv", false, (string[] paths) => { CheckSetList(paths); });
        }

        public void CreateList()
        {
            SFB.StandaloneFileBrowser.SaveFilePanelAsync("Create participant list", currentFolder, "participant_list", "csv", (string path) => { CheckSetList(path); });
        }


        public void CheckSetList(string[] paths)
        {
            if (paths.Length == 0) { return; }
            CheckSetList(paths[0]);
        }


        public void CheckSetList(string path)
        {
            ppListPath = path;

            ppListNameDisplay.text = ppListPath;
            ppListNameDisplay.color = Color.black;
            currentFolder = Directory.GetParent(ppListPath).ToString();

            GetCheckParticipantList();

        }

        public void GetCheckParticipantList()
        {
            experiment.ReadCSVFile(ppListPath, new System.Action<DataTable>((data) => SetPPList(data)));
        }


        void CreateNewPPList(string filePath)
        {
            // create example table
            DataTable exampleData = new DataTable();

            // create headers
            foreach (var header in startup.participantDataPoints.Select(x => x.internalName))
            {
                exampleData.Columns.Add(new DataColumn(header, typeof(string)));
            }            

            // create example row
            DataRow row1 = exampleData.NewRow();
            foreach (var dataPoint in startup.participantDataPoints)
            {
                row1[dataPoint.internalName] = dataPoint.controller.GetDefault();
            }
            row1["ppid"] = "example001";

            exampleData.Rows.Add(row1);

            // save
            experiment.WriteCSVFile(exampleData, filePath);

            // re-read it back in
            GetCheckParticipantList();
        }


        public void SetPPList(DataTable data)
        {
            ppList = data;
            if (ppList == null)
            {
                Popup pplistAttention = new Popup();
                pplistAttention.messageType = MessageType.Attention;
                pplistAttention.message = string.Format("An empty participant list will be created at {0}. Data you collect will be stored in the same folder as this list.", ppListPath);
                pplistAttention.onOK = new System.Action( () => {CreateNewPPList(ppListPath);}) ;
                popupController.DisplayPopup(pplistAttention);
                return;
            }

            Debug.Log(string.Format("Loaded: {0}", ppListPath));

            List<string> participants = ppList.AsEnumerable().Select(x => x[0].ToString()).ToList();
            participantSelector.SetParticipants(participants);
            participantSelector.SelectNew();

            // enable selector
            ExperimentStartupController.SetSelectableAndChildrenInteractable(participantSelector.gameObject, true);
            // enable form
            ExperimentStartupController.SetSelectableAndChildrenInteractable(form.gameObject, true);
            // enable start button
            ExperimentStartupController.SetSelectableAndChildrenInteractable(startButton.gameObject, true);

        }


        public void UpdateFormByPPID(string ppid)
        {
            DataRow row = ppList.AsEnumerable().Single(r => r.Field<string>("ppid") == ppid);

            foreach (var dataPoint in startup.participantDataPoints)
            {
                try
                {
                    print(dataPoint.internalName);
                    dataPoint.controller.SetContents(row[dataPoint.internalName]);
                }
                catch (ArgumentException e)
                {
                    string s = string.Format("Column '{0}' not found in data table - It will be added with empty values", dataPoint.internalName);
                    Debug.LogWarning(s);
                    
                    ppList.Columns.Add(new DataColumn(dataPoint.internalName, typeof(string)));
                    dataPoint.controller.Clear();
                }
            }
        }


        public string Finish()
        {
            // get completed information form
            var completedForm = form.GetCompletedForm();

            if (completedForm == null)
                throw new Exception("Form not completed correctly!");

            // get PPID and set to safe name
            string ppid = completedForm["ppid"].ToString();
            ppid = Extensions.GetSafeFilename(ppid);

            // check if not empty
            if (ppid.Replace(" ", string.Empty) == string.Empty)
            {
                form.ppidElement.controller.DisplayFault();
                throw new Exception("Invalid participant name!");
            }

            DataRow row;
            // if we have new participant selected, we need to create it in the pplist
            if (participantSelector.IsNewSelected())
            {
                // add new participant to list
                row = ppList.NewRow();
                ppList.Rows.Add(row);
            }
            // else we update the row with any changes we made in the form
            else 
            {
                string oldPpid = participantSelector.participantDropdown.GetContents().ToString();
                // update row
                row = ppList.AsEnumerable().Single(r => r.Field<string>("ppid") == oldPpid);
            }

            // update row in table
            foreach (var keyValuePair in completedForm)
                row[keyValuePair.Key] = keyValuePair.Value;     

            // write pplist
            CommitCSV();
            return ppid;

        }

        public void UpdateDatapoint(string ppid, string datapointName, object value)
        {
            DataRow row = ppList.AsEnumerable().Single(r => r.Field<string>("ppid") == ppid);
            
            try
            {
                row[datapointName] = value;
            }
            catch (ArgumentException e)
            {
                string s = string.Format("Column '{0}' not found in data table - It will be added with empty values", e.ParamName);
                Debug.LogWarning(s);
                ppList.Columns.Add(new DataColumn(datapointName, typeof(string)));
                row[datapointName] = value;
            }
            
            
        }

        public void CommitCSV()
        {
            experiment.WriteCSVFile(ppList, ppListPath);
            Debug.Log(string.Format("Updating: {0}", ppListPath));
            experiment.participantDetails = GenerateDict();
        }

        public Dictionary<string, object> GenerateDict()
        {
            return form.GetCompletedForm();
        }

    }
}