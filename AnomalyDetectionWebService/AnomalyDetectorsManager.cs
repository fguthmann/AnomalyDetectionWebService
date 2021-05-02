using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection;
using AnomalyAlgorithm;
using System.Threading.Tasks;

namespace AnomalyDetectionWebService
{
    // manager class for IAnomalyDetector
    // safe-thread class
    // L_var should be access only within "lock (L_var){}" and for SHORT time lock!
    public class AnomalyDetectorsManager
    {
        private Dictionary<int, MODEL> L_NormalModels = new Dictionary<int, MODEL>();

        public bool IsReady(int modelId)
        {
            lock (L_NormalModels)
            {
                return L_NormalModels.ContainsKey(modelId) && L_NormalModels[modelId].status == MODEL.Status_Ready;
            }
        }
        public bool IsExist(int modelId)
        {
            lock (L_NormalModels)
            {
                return L_NormalModels.ContainsKey(modelId);
            }
        }
        public bool IsPending(int modelId)
        {
            lock (L_NormalModels)
            {
                return L_NormalModels.ContainsKey(modelId) && L_NormalModels[modelId].status == MODEL.Status_Pending;
            }
        }
        public MODEL LearnAndAddNewModel(string detectoionType, Train_Data data, Action afterFinishingLearning) {
            MODEL model;
            int id = 3456;
            lock (L_NormalModels)
            {
                while (L_NormalModels.ContainsKey(id))
                    id = new Random().Next();
                model = new MODEL() { model_id = id, status = MODEL.Status_Pending,
                                            upload_time = DateTime.Now };
                L_NormalModels.Add(id,model);
            }
            Task.Run(() => {
                try
                {
                    var correlation = AnomalyDetection.GetNormal(data.train_data, detectoionType);
                    IO_Util.SaveNormalModel(model.CSVFileName(), correlation);
                    lock (L_NormalModels)
                    {
                        if (L_NormalModels.ContainsKey(id)) L_NormalModels[id].status = MODEL.Status_Ready;
                    }
                } catch {
                    lock (L_NormalModels)
                    {
                        if (L_NormalModels.ContainsKey(id)) L_NormalModels[id].status = MODEL.Status_Corrupted;
                    }
                  }
                afterFinishingLearning();
                });
            return model;
         }

        public ANOMALY Detect(int idModel, Predict_Data data)
        {
            string csvFile = "";
            lock(L_NormalModels)
            {
                if (L_NormalModels.ContainsKey(idModel) && L_NormalModels[idModel].status == MODEL.Status_Ready)
                    csvFile = L_NormalModels[idModel].CSVFileName();
            }
            if (String.IsNullOrWhiteSpace(csvFile)) return null;
            var correlation = IO_Util.LoadNormalModel(csvFile);
            if (correlation == null) return null;
            try
            {
                var detection = AnomalyDetection.GetDetection(data.predict_data, correlation);
                if (detection == null) return null;
                Dictionary<string, string> reason = AnomalyDetection.GetReportTypes(correlation, detection);
                Dictionary<String, List<Span>> spanDictionary = AnomalyDetection.ToSpanDictionary(detection);
                if (reason == null || spanDictionary == null) return null;
                return new ANOMALY() { anomalies = spanDictionary, reason = reason };
            }catch
            {
                return null;
            }

        }

        public MODEL getIdModel(int id)
        {
            lock (L_NormalModels)
            {
                if (L_NormalModels.ContainsKey(id))
                    return L_NormalModels[id];
                else
                    return null;
            }
        }
        public bool Remove(int id)
        {
            lock(L_NormalModels)
            {
                if (L_NormalModels.ContainsKey(id))
                {
                    L_NormalModels.Remove(id);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        public List<MODEL> GetNormalModels()
        {
            lock (L_NormalModels)
            {
                var list = new List<MODEL>();
                foreach (var model in L_NormalModels.Values)
                        list.Add(model);
                return list;
            }
        }
    }
}