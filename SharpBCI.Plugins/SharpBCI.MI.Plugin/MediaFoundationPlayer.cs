using MarukoLib.Interop;
using MediaFoundation;
using MediaFoundation.EVR;
using MediaFoundation.Misc;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace SharpBCI.Paradigms.MI
{

    internal class MediaFoundationPlayer : COMBase, IMFAsyncCallback
    {

        #region Declarations

        const int WM_APP = 0x8000;
        const int WM_APP_ERROR = WM_APP + 2;
        const int WM_APP_NOTIFY = WM_APP + 1;
        const int WAIT_TIMEOUT = 258;

        const int MF_VERSION = 0x10070;

        public enum PlayerState
        {
            Ready = 0,
            OpenPending,
            Started,
            PausePending,
            Paused,
            StartPending,
        }

        #endregion

        public MediaFoundationPlayer(IntPtr hVideo, IntPtr hEvent)
        {
            TRACE(("CPlayer::CPlayer"));

            Debug.Assert(hVideo != IntPtr.Zero);
            Debug.Assert(hEvent != IntPtr.Zero);

            m_pSession = null;
            m_pSource = null;
            m_pVideoDisplay = null;
            m_hwndVideo = hVideo;
            m_hwndEvent = hEvent;
            m_state = PlayerState.Ready;

            m_hCloseEvent = new AutoResetEvent(false);

            MFError throwonhr = MFExtern.MFStartup(0x10070, MFStartup.Full);
        }

#if DEBUG
        // Destructor is private. Caller should call Release.
        ~MediaFoundationPlayer()
        {
            Debug.Assert(m_pSession == null);  // If FALSE, the app did not call Shutdown().
        }
#endif

        #region Public methods

        public HResult OpenURL(string sURL)
        {
            TRACE("CPlayer::OpenURL");
            TRACE("URL = " + sURL);

            // 1. Create a new media session.
            // 2. Create the media source.
            // 3. Create the topology.
            // 4. Queue the topology [asynchronous]
            // 5. Start playback [asynchronous - does not happen in this method.]

            HResult hr = HResult.S_OK;
            try
            {
                IMFTopology pTopology = null;

                // Create the media session.
                CreateSession();

                // Create the media source.
                CreateMediaSource(sURL);

                // Create a partial topology.
                CreateTopologyFromSource(out pTopology);

                // Set the topology on the media session.
                hr = m_pSession.SetTopology(0, pTopology);
                MFError.ThrowExceptionForHR(hr);

                // Set our state to "open pending"
                m_state = PlayerState.OpenPending;
                NotifyState();

                SafeRelease(pTopology);

                // If SetTopology succeeded, the media session will queue an
                // MESessionTopologySet event.
            }
            catch (Exception ce)
            {
                hr = (HResult)Marshal.GetHRForException(ce);
                NotifyError(hr);
                m_state = PlayerState.Ready;
            }

            return hr;
        }

        public HResult Play()
        {
            TRACE("CPlayer::Play");

            if (m_state != PlayerState.Paused)
            {
                return HResult.E_FAIL;
            }
            if (m_pSession == null || m_pSource == null)
            {
                return HResult.E_UNEXPECTED;
            }

            HResult hr = HResult.S_OK;

            try
            {
                StartPlayback();

                m_state = PlayerState.StartPending;
                NotifyState();
            }
            catch (Exception ce)
            {
                hr = (HResult)Marshal.GetHRForException(ce);
                NotifyError(hr);
            }

            return hr;
        }

        public HResult Pause()
        {
            TRACE("CPlayer::Pause");

            if (m_state != PlayerState.Started)
            {
                return HResult.E_FAIL;
            }
            if (m_pSession == null || m_pSource == null)
            {
                return HResult.E_UNEXPECTED;
            }

            HResult hr = HResult.S_OK;

            try
            {
                hr = m_pSession.Pause();
                MFError.ThrowExceptionForHR(hr);

                m_state = PlayerState.PausePending;
                NotifyState();
            }
            catch (Exception ce)
            {
                hr = (HResult)Marshal.GetHRForException(ce);
                NotifyError(hr);
            }

            return hr;
        }

        public HResult Shutdown()
        {
            TRACE("CPlayer::ShutDown");

            HResult hr = HResult.S_OK;

            try
            {
                if (m_hCloseEvent != null)
                {
                    // Close the session
                    CloseSession();

                    // Shutdown the Media Foundation platform
                    hr = MFExtern.MFShutdown();
                    MFError.ThrowExceptionForHR(hr);

                    m_hCloseEvent.Close();
                    m_hCloseEvent = null;
                }
            }
            catch (Exception ce)
            {
                hr = (HResult)Marshal.GetHRForException(ce);
            }

            return hr;
        }

        // Video functionality
        public HResult Repaint()
        {
            HResult hr = HResult.S_OK;

            if (m_pVideoDisplay != null)
            {
                try
                {
                    hr = m_pVideoDisplay.RepaintVideo();
                    MFError.ThrowExceptionForHR(hr);
                }
                catch (Exception ce)
                {
                    hr = (HResult)Marshal.GetHRForException(ce);
                }
            }

            return hr;
        }

        public HResult ResizeVideo(short width, short height)
        {
            HResult hr = HResult.S_OK;
            TRACE(string.Format("ResizeVideo: {0}x{1}", width, height));

            if (m_pVideoDisplay != null)
            {
                try
                {
                    var nRect = new MFVideoNormalizedRect
                    {
                        left = 0,
                        right = 1,
                        top = 0,
                        bottom = 1
                    };
                    var rcDest = new MFRect
                    {
                        left = 0,
                        top = 0,
                        right = width,
                        bottom = height
                    };

                    hr = m_pVideoDisplay.SetVideoPosition(nRect, rcDest);
                    MFError.ThrowExceptionForHR(hr);
                }
                catch (Exception ce)
                {
                    hr = (HResult)Marshal.GetHRForException(ce);
                }
            }

            return hr;
        }

        public PlayerState GetState()
        {
            return m_state;
        }

        public bool HasVideo()
        {
            return (m_pVideoDisplay != null);
        }

        #endregion

        #region IMFAsyncCallback Members

        HResult IMFAsyncCallback.GetParameters(out MFASync pdwFlags, out MFAsyncCallbackQueue pdwQueue)
        {
            pdwFlags = MFASync.FastIOProcessingCallback;
            pdwQueue = MFAsyncCallbackQueue.Standard;
            //throw new COMException("IMFAsyncCallback.GetParameters not implemented in Player", E_NotImplemented);

            return HResult.S_OK;
        }

        HResult IMFAsyncCallback.Invoke(IMFAsyncResult pResult)
        {
            MFError throwonhr;
            IMFMediaEvent pEvent = null;
            MediaEventType meType = MediaEventType.MEUnknown;  // Event type
            HResult hrStatus = 0;           // Event status
            MFTopoStatus TopoStatus = MFTopoStatus.Invalid; // Used with MESessionTopologyStatus event.

            try
            {
                // Get the event from the event queue.
                throwonhr = m_pSession.EndGetEvent(pResult, out pEvent);

                // Get the event type.
                throwonhr = pEvent.GetType(out meType);

                // Get the event status. If the operation that triggered the event did
                // not succeed, the status is a failure code.
                throwonhr = pEvent.GetStatus(out hrStatus);

                TRACE(string.Format("Media event: " + meType.ToString()));

                // Check if the async operation succeeded.
                if (Succeeded(hrStatus))
                {
                    // Switch on the event type. Update the internal state of the CPlayer as needed.
                    switch (meType)
                    {
                        case MediaEventType.MESessionTopologyStatus:
                            // Get the status code.
                            int i;
                            throwonhr = pEvent.GetUINT32(MFAttributesClsid.MF_EVENT_TOPOLOGY_STATUS, out i);
                            TopoStatus = (MFTopoStatus)i;
                            switch (TopoStatus)
                            {
                                case MFTopoStatus.Ready:
                                    OnTopologyReady(pEvent);
                                    break;
                                default:
                                    // Nothing to do.
                                    break;
                            }
                            break;

                        case MediaEventType.MESessionStarted:
                            OnSessionStarted(pEvent);
                            break;

                        case MediaEventType.MESessionPaused:
                            OnSessionPaused(pEvent);
                            break;

                        case MediaEventType.MESessionClosed:
                            OnSessionClosed(pEvent);
                            break;

                        case MediaEventType.MEEndOfPresentation:
                            OnPresentationEnded(pEvent);
                            break;
                    }
                }
                else
                {
                    // The async operation failed. Notify the application
                    NotifyError(hrStatus);
                }
            }
            finally
            {
                // Request another event.
                if (meType != MediaEventType.MESessionClosed)
                {
                    throwonhr = m_pSession.BeginGetEvent(this, null);
                }

                SafeRelease(pEvent);
            }

            return HResult.S_OK;
        }

        #endregion

        #region Protected methods

        // NotifyState: Notifies the application when the state changes.
        protected void NotifyState()
        {
            User32.PostMessage(m_hwndEvent, WM_APP_NOTIFY, new IntPtr((int)m_state), IntPtr.Zero);
        }

        // NotifyState: Notifies the application when an error occurs.
        protected void NotifyError(HResult hr)
        {
            int hr2 = (int)hr;
            TRACE("NotifyError: 0x" + hr2.ToString("X"));
            m_state = PlayerState.Ready;
            User32.PostMessage(m_hwndEvent, WM_APP_ERROR, new IntPtr(hr2), IntPtr.Zero);
        }

        protected void CreateSession()
        {
            // Close the old session, if any.
            CloseSession();

            // Create the media session.
            MFError throwonhr = MFExtern.MFCreateMediaSession(null, out m_pSession);

            // Start pulling events from the media session
            throwonhr = m_pSession.BeginGetEvent(this, null);
        }

        protected void CloseSession()
        {
            MFError throwonhr;

            if (m_pVideoDisplay != null)
            {
                Marshal.ReleaseComObject(m_pVideoDisplay);
                m_pVideoDisplay = null;
            }

            if (m_pSession != null)
            {
                throwonhr = m_pSession.Close();

                // Wait for the close operation to complete
                bool res = m_hCloseEvent.WaitOne(5000, true);
                if (!res)
                {
                    TRACE(("WaitForSingleObject timed out!"));
                }
            }

            // Complete shutdown operations

            // 1. Shut down the media source
            if (m_pSource != null)
            {
                throwonhr = m_pSource.Shutdown();
                SafeRelease(m_pSource);
                m_pSource = null;
            }

            // 2. Shut down the media session. (Synchronous operation, no events.)
            if (m_pSession != null)
            {
                throwonhr = m_pSession.Shutdown();
                Marshal.ReleaseComObject(m_pSession);
                m_pSession = null;
            }
        }

        protected void StartPlayback()
        {
            TRACE("CPlayer::StartPlayback");

            Debug.Assert(m_pSession != null);

            HResult hr = m_pSession.Start(Guid.Empty, new PropVariant());
            MFError.ThrowExceptionForHR(hr);
        }

        protected void CreateMediaSource(string sURL)
        {
            TRACE("CPlayer::CreateMediaSource");

            IMFSourceResolver pSourceResolver;
            object pSource;

            // Create the source resolver.
            HResult hr = MFExtern.MFCreateSourceResolver(out pSourceResolver);
            MFError.ThrowExceptionForHR(hr);

            try
            {
                // Use the source resolver to create the media source.
                MFObjectType ObjectType = MFObjectType.Invalid;

                hr = pSourceResolver.CreateObjectFromURL(
                        sURL,                       // URL of the source.
                        MFResolution.MediaSource,   // Create a source object.
                        null,                       // Optional property store.
                        out ObjectType,             // Receives the created object type.
                        out pSource                 // Receives a pointer to the media source.
                    );
                MFError.ThrowExceptionForHR(hr);

                // Get the IMFMediaSource interface from the media source.
                m_pSource = (IMFMediaSource)pSource;
            }
            finally
            {
                // Clean up
                Marshal.ReleaseComObject(pSourceResolver);
            }
        }

        protected void CreateTopologyFromSource(out IMFTopology ppTopology)
        {
            TRACE("CPlayer::CreateTopologyFromSource");

            Debug.Assert(m_pSession != null);
            Debug.Assert(m_pSource != null);

            IMFTopology pTopology = null;
            IMFPresentationDescriptor pSourcePD = null;
            int cSourceStreams = 0;

            MFError throwonhr;

            try
            {
                // Create a new topology.
                throwonhr = MFExtern.MFCreateTopology(out pTopology);

                // Create the presentation descriptor for the media source.
                throwonhr = m_pSource.CreatePresentationDescriptor(out pSourcePD);

                // Get the number of streams in the media source.
                throwonhr = pSourcePD.GetStreamDescriptorCount(out cSourceStreams);

                TRACE(string.Format("Stream count: {0}", cSourceStreams));

                // For each stream, create the topology nodes and add them to the topology.
                for (int i = 0; i < cSourceStreams; i++)
                {
                    AddBranchToPartialTopology(pTopology, pSourcePD, i);
                }

                // Return the IMFTopology pointer to the caller.
                ppTopology = pTopology;
            }
            catch
            {
                // If we failed, release the topology
                SafeRelease(pTopology);
                throw;
            }
            finally
            {
                SafeRelease(pSourcePD);
            }
        }

        protected void AddBranchToPartialTopology(
            IMFTopology pTopology,
            IMFPresentationDescriptor pSourcePD,
            int iStream
            )
        {
            MFError throwonhr;

            TRACE("CPlayer::AddBranchToPartialTopology");

            Debug.Assert(pTopology != null);

            IMFStreamDescriptor pSourceSD = null;
            IMFTopologyNode pSourceNode = null;
            IMFTopologyNode pOutputNode = null;
            bool fSelected = false;

            try
            {
                // Get the stream descriptor for this stream.
                throwonhr = pSourcePD.GetStreamDescriptorByIndex(iStream, out fSelected, out pSourceSD);

                // Create the topology branch only if the stream is selected.
                // Otherwise, do nothing.
                if (fSelected)
                {
                    // Create a source node for this stream.
                    CreateSourceStreamNode(pSourcePD, pSourceSD, out pSourceNode);

                    // Create the output node for the renderer.
                    CreateOutputNode(pSourceSD, out pOutputNode);

                    // Add both nodes to the topology.
                    throwonhr = pTopology.AddNode(pSourceNode);
                    throwonhr = pTopology.AddNode(pOutputNode);

                    // Connect the source node to the output node.
                    throwonhr = pSourceNode.ConnectOutput(0, pOutputNode, 0);
                }
            }
            finally
            {
                // Clean up.
                SafeRelease(pSourceSD);
                SafeRelease(pSourceNode);
                SafeRelease(pOutputNode);
            }
        }

        protected void CreateSourceStreamNode(
            IMFPresentationDescriptor pSourcePD,
            IMFStreamDescriptor pSourceSD,
            out IMFTopologyNode ppNode
            )
        {
            Debug.Assert(m_pSource != null);

            MFError throwonhr;
            IMFTopologyNode pNode = null;

            try
            {
                // Create the source-stream node.
                throwonhr = MFExtern.MFCreateTopologyNode(MFTopologyType.SourcestreamNode, out pNode);

                // Set attribute: Pointer to the media source.
                throwonhr = pNode.SetUnknown(MFAttributesClsid.MF_TOPONODE_SOURCE, m_pSource);

                // Set attribute: Pointer to the presentation descriptor.
                throwonhr = pNode.SetUnknown(MFAttributesClsid.MF_TOPONODE_PRESENTATION_DESCRIPTOR, pSourcePD);

                // Set attribute: Pointer to the stream descriptor.
                throwonhr = pNode.SetUnknown(MFAttributesClsid.MF_TOPONODE_STREAM_DESCRIPTOR, pSourceSD);

                // Return the IMFTopologyNode pointer to the caller.
                ppNode = pNode;
            }
            catch
            {
                // If we failed, release the pnode
                SafeRelease(pNode);
                throw;
            }
        }

        protected void CreateOutputNode(
            IMFStreamDescriptor pSourceSD,
            out IMFTopologyNode ppNode
            )
        {
            IMFTopologyNode pNode = null;
            IMFMediaTypeHandler pHandler = null;
            IMFActivate pRendererActivate = null;

            Guid guidMajorType = Guid.Empty;
            MFError throwonhr;

            // Get the stream ID.
            int streamID = 0;

            try
            {
                HResult hr;

                hr = pSourceSD.GetStreamIdentifier(out streamID); // Just for debugging, ignore any failures.
                if (MFError.Failed(hr))
                {
                    TRACE("IMFStreamDescriptor::GetStreamIdentifier" + hr.ToString());
                }

                // Get the media type handler for the stream.
                throwonhr = pSourceSD.GetMediaTypeHandler(out pHandler);

                // Get the major media type.
                throwonhr = pHandler.GetMajorType(out guidMajorType);

                // Create a downstream node.
                throwonhr = MFExtern.MFCreateTopologyNode(MFTopologyType.OutputNode, out pNode);

                // Create an IMFActivate object for the renderer, based on the media type.
                if (MFMediaType.Audio == guidMajorType)
                {
                    // Create the audio renderer.
                    TRACE(string.Format("Stream {0}: audio stream", streamID));
                    throwonhr = MFExtern.MFCreateAudioRendererActivate(out pRendererActivate);
                }
                else if (MFMediaType.Video == guidMajorType)
                {
                    // Create the video renderer.
                    TRACE(string.Format("Stream {0}: video stream", streamID));
                    throwonhr = MFExtern.MFCreateVideoRendererActivate(m_hwndVideo, out pRendererActivate);
                }
                else
                {
                    TRACE(string.Format("Stream {0}: Unknown format", streamID));
                    throw new COMException("Unknown format", (int)HResult.E_FAIL);
                }

                // Set the IActivate object on the output node.
                throwonhr = pNode.SetObject(pRendererActivate);

                // Return the IMFTopologyNode pointer to the caller.
                ppNode = pNode;
            }
            catch
            {
                // If we failed, release the pNode
                SafeRelease(pNode);
                throw;
            }
            finally
            {
                // Clean up.
                SafeRelease(pHandler);
                SafeRelease(pRendererActivate);
            }
        }

        // Media event handlers
        protected void OnTopologyReady(IMFMediaEvent pEvent)
        {
            HResult hr;
            object o;
            TRACE("CPlayer::OnTopologyReady");

            // Ask for the IMFVideoDisplayControl interface.
            // This interface is implemented by the EVR and is
            // exposed by the media session as a service.

            // Note: This call is expected to fail if the source
            // does not have video.

            try
            {
                hr = MFExtern.MFGetService(
                    m_pSession,
                    MFServices.MR_VIDEO_RENDER_SERVICE,
                    typeof(IMFVideoDisplayControl).GUID,
                    out o
                    );
                MFError.ThrowExceptionForHR(hr);

                m_pVideoDisplay = o as IMFVideoDisplayControl;
            }
            catch (InvalidCastException)
            {
                m_pVideoDisplay = null;
            }

            try
            {
                StartPlayback();
            }
            catch (Exception ce)
            {
                hr = (HResult)Marshal.GetHRForException(ce);
                NotifyError(hr);
            }

            // If we succeeded, the Start call is pending. Don't notify the app yet.
        }

        protected void OnSessionStarted(IMFMediaEvent pEvent)
        {
            TRACE("CPlayer::OnSessionStarted");

            m_state = PlayerState.Started;
            NotifyState();
        }

        protected void OnSessionPaused(IMFMediaEvent pEvent)
        {
            TRACE("CPlayer::OnSessionPaused");

            m_state = PlayerState.Paused;
            NotifyState();
        }

        protected void OnSessionClosed(IMFMediaEvent pEvent)
        {
            TRACE("CPlayer::OnSessionClosed");

            // The application thread is waiting on this event, inside the
            // CPlayer::CloseSession method.
            m_hCloseEvent.Set();
        }

        protected void OnPresentationEnded(IMFMediaEvent pEvent)
        {
            TRACE("CPlayer::OnPresentationEnded");

            // The session puts itself into the stopped state autmoatically.

            m_state = PlayerState.Ready;
            NotifyState();
        }

        #endregion

        #region Member Variables

        protected IMFMediaSession m_pSession;
        protected IMFMediaSource m_pSource;
        protected IMFVideoDisplayControl m_pVideoDisplay;

        protected IntPtr m_hwndVideo;       // Video window.
        protected IntPtr m_hwndEvent;       // App window to receive events.
        protected PlayerState m_state;          // Current state of the media session.
        protected AutoResetEvent m_hCloseEvent;     // Event to wait on while closing

        #endregion
    }


}
