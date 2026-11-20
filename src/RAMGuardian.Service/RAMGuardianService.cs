using System;
using System.ServiceProcess;
using RAMGuardian.Logging;

namespace RAMGuardian.Service
{
    public class RAMGuardianService : ServiceBase
    {
        private MonitoringEngine _engine;
        private readonly FileLogger _logger;

        public const string ServiceNameIdentifier = "RAMGuardian";
        public const string ServiceDisplayName = "RAM Guardian Resource Monitor";

        public RAMGuardianService()
        {
            ServiceName = ServiceNameIdentifier;
            CanStop = true;
            CanPauseAndContinue = true;
            CanShutdown = true;
            AutoLog = false; // We use our own high-performance structured FileLogger

            _logger = new FileLogger();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                _logger.Info("SERVICE_START", "RAM Guardian Windows Service starting.");
                _engine = new MonitoringEngine(15);
                _engine.Start();
                _logger.Info("SERVICE_STARTED", "RAM Guardian Windows Service started successfully.");
            }
            catch (Exception ex)
            {
                _logger.Critical("SERVICE_START_FAILED", "Failed to start RAM Guardian Windows Service", ex);
                throw;
            }
        }

        protected override void OnStop()
        {
            try
            {
                _logger.Info("SERVICE_STOP", "RAM Guardian Windows Service stopping.");
                if (_engine != null)
                {
                    _engine.Stop();
                    _engine.Dispose();
                    _engine = null;
                }
                _logger.Info("SERVICE_STOPPED", "RAM Guardian Windows Service stopped cleanly.");
            }
            catch (Exception ex)
            {
                _logger.Error("SERVICE_STOP_ERROR", "Error during service stop", ex);
            }
        }

        protected override void OnPause()
        {
            try
            {
                _logger.Info("SERVICE_PAUSE", "RAM Guardian Windows Service pausing.");
                if (_engine != null)
                {
                    _engine.Stop();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("SERVICE_PAUSE_ERROR", "Error during service pause", ex);
            }
        }

        protected override void OnContinue()
        {
            try
            {
                _logger.Info("SERVICE_RESUME", "RAM Guardian Windows Service resuming.");
                if (_engine != null)
                {
                    _engine.Start();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("SERVICE_RESUME_ERROR", "Error during service resume", ex);
            }
        }

        protected override void OnShutdown()
        {
            try
            {
                _logger.Info("SERVICE_SHUTDOWN", "System shutdown detected. Cleanly stopping RAM Guardian Service.");
                OnStop();
            }
            catch { }
        }
    }
}
