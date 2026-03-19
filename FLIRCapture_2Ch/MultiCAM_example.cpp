//=============================================================================
// Copyright (c) 2025 FLIR Integrated Imaging Solutions, Inc. All Rights Reserved.
//
// This software is the confidential and proprietary information of FLIR
// Integrated Imaging Solutions, Inc. ("Confidential Information"). You
// shall not disclose such Confidential Information and shall use it only in
// accordance with the terms of the license agreement you entered into
// with FLIR Integrated Imaging Solutions, Inc. (FLIR).
//
// FLIR MAKES NO REPRESENTATIONS OR WARRANTIES ABOUT THE SUITABILITY OF THE
// SOFTWARE, EITHER EXPRESSED OR IMPLIED, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE, OR NON-INFRINGEMENT. FLIR SHALL NOT BE LIABLE FOR ANY DAMAGES
// SUFFERED BY LICENSEE AS A RESULT OF USING, MODIFYING OR DISTRIBUTING
// THIS SOFTWARE OR ITS DERIVATIVES.
//=============================================================================
 
#include "Spinnaker.h"
#include "SpinGenApi/SpinnakerGenApi.h"
#include <iostream>
#include <sstream>
 
#ifndef _WIN32
#include <pthread.h>
#endif
 
using namespace Spinnaker;
using namespace Spinnaker::GenApi;
using namespace Spinnaker::GenICam;
using namespace std;
 
// This function prints the device information of the camera from the transport
// layer; please see NodeMapInfo example for more in-depth comments on printing
// device information from the nodemap.
int PrintDeviceInfo(INodeMap& nodeMap, std::string camSerial)
{
    int result = 0;
 
    cout << "[" << camSerial << "] Printing device information ..." << endl << endl;
 
    FeatureList_t features;
    CCategoryPtr category = nodeMap.GetNode("DeviceInformation");
    if (IsReadable(category))
    {
        category->GetFeatures(features);
 
        FeatureList_t::const_iterator it;
        for (it = features.begin(); it != features.end(); ++it)
        {
            CNodePtr pfeatureNode = *it;
            CValuePtr pValue = (CValuePtr)pfeatureNode;
            cout << "[" << camSerial << "] " << pfeatureNode->GetName() << " : "
                 << (IsReadable(pValue) ? pValue->ToString() : "Node not readable") << endl;
        }
    }
    else
    {
        cout << "[" << camSerial << "] "
             << "Device control information not readable." << endl;
    }
 
    cout << endl;
 
    return result;
}
 
// Disables or enables heartbeat on GEV cameras so debugging does not incur timeout errors
int ConfigureGVCPHeartbeat(CameraPtr pCam, bool enableHeartbeat)
{
    //
    // Write to boolean node controlling the camera's heartbeat
    //
    // *** NOTES ***
    // This applies only to GEV cameras.
    //
    // GEV cameras have a heartbeat built in, but when debugging applications the
    // camera may time out due to its heartbeat. Disabling the heartbeat prevents
    // this timeout from occurring, enabling us to continue with any necessary 
    // debugging.
    //
    // *** LATER ***
    // Make sure that the heartbeat is reset upon completion of the debugging.  
    // If the application is terminated unexpectedly, the camera may not locked
    // to Spinnaker indefinitely due to the the timeout being disabled.  When that 
    // happens, a camera power cycle will reset the heartbeat to its default setting.
 
    // Retrieve TL device nodemap
    INodeMap& nodeMapTLDevice = pCam->GetTLDeviceNodeMap();
 
    // Retrieve GenICam nodemap
    INodeMap& nodeMap = pCam->GetNodeMap();
 
    CEnumerationPtr ptrDeviceType = nodeMapTLDevice.GetNode("DeviceType");
    if (!IsReadable(ptrDeviceType))
    {
        return -1;
    }
 
    if (ptrDeviceType->GetIntValue() != DeviceType_GigEVision)
    {
        return 0;
    }
 
    if (enableHeartbeat)
    {
        cout << endl << "Resetting heartbeat..." << endl << endl;
    }
    else
    {
        cout << endl << "Disabling heartbeat..." << endl << endl;
    }
 
    CBooleanPtr ptrDeviceHeartbeat = nodeMap.GetNode("GevGVCPHeartbeatDisable");
    if (!IsWritable(ptrDeviceHeartbeat))
    {
        cout << "Unable to configure heartbeat. Continuing with execution as this may be non-fatal..."
            << endl
            << endl;
    }
    else
    {
        ptrDeviceHeartbeat->SetValue(!enableHeartbeat);
 
        if (!enableHeartbeat)
        {
            cout << "WARNING: Heartbeat has been disabled for the rest of this example run." << endl;
            cout << "         Heartbeat will be reset upon the completion of this run.  If the " << endl;
            cout << "         example is aborted unexpectedly before the heartbeat is reset, the" << endl;
            cout << "         camera may need to be power cycled to reset the heartbeat." << endl << endl;
        }
        else
        {
            cout << "Heartbeat has been reset." << endl;
        }
    }
 
    return 0;
}
 
int ResetGVCPHeartbeat(CameraPtr pCam)
{
    return ConfigureGVCPHeartbeat(pCam, true);
}
 
int DisableGVCPHeartbeat(CameraPtr pCam)
{
    return ConfigureGVCPHeartbeat(pCam, false);
}
 
// This function acquires and saves 10 images from a camera.
#if defined(_WIN32)
DWORD WINAPI AcquireImages(LPVOID lpParam)
{
    CameraPtr pCam = *((CameraPtr*)lpParam);
#else
void* AcquireImages(void* arg)
{
    CameraPtr pCam = *((CameraPtr*)arg);
#endif
 
    try
    {
        // Retrieve TL device nodemap
        INodeMap& nodeMapTLDevice = pCam->GetTLDeviceNodeMap();
 
        // Retrieve device serial number for filename
        CStringPtr ptrStringSerial = nodeMapTLDevice.GetNode("DeviceSerialNumber");
 
        std::string serialNumber = "";
 
        if (IsReadable(ptrStringSerial))
        {
            serialNumber = ptrStringSerial->GetValue();
        }
 
        cout << endl
             << "[" << serialNumber << "] "
             << "*** IMAGE ACQUISITION THREAD STARTING"
             << " ***" << endl
             << endl;
 
        // Print device information
        PrintDeviceInfo(nodeMapTLDevice, serialNumber);
 
        // Initialize camera
        pCam->Init();
 
        // Retrieve nodemap
        INodeMap& nodeMap = pCam->GetNodeMap();
 
#ifdef _DEBUG
        // Disable heartbeat for GEV camera for Debug mode
        if (DisableGVCPHeartbeat(pCam) != 0)
#else
        // Reset heartbeat for GEV camera for Release mode
        if (ResetGVCPHeartbeat(pCam) != 0)
#endif
        {
#if defined(_WIN32)
            return 0;
#else
            return (void*)0;
#endif
        }
 
        // Set acquisition mode to continuous
        CEnumerationPtr ptrAcquisitionMode = nodeMap.GetNode("AcquisitionMode");
        if (!IsReadable(ptrAcquisitionMode) ||
            !IsWritable(ptrAcquisitionMode))
        {
            cout << "Unable to set acquisition mode to continuous (node retrieval; camera " << serialNumber
                 << "). Aborting..." << endl
                 << endl;
#if defined(_WIN32)
            return 0;
#else
            return (void*)0;
#endif
        }
 
        CEnumEntryPtr ptrAcquisitionModeContinuous = ptrAcquisitionMode->GetEntryByName("Continuous");
        if (!IsReadable(ptrAcquisitionModeContinuous))
        {
            cout << "Unable to get acquisition mode to continuous (entry 'continuous' retrieval " << serialNumber
                 << "). Aborting..." << endl
                 << endl;
#if defined(_WIN32)
            return 0;
#else
            return (void*)0;
#endif
        }
 
        int64_t acquisitionModeContinuous = ptrAcquisitionModeContinuous->GetValue();
 
        ptrAcquisitionMode->SetIntValue(acquisitionModeContinuous);
 
        cout << "[" << serialNumber << "] "
             << "Acquisition mode set to continuous..." << endl;
 
        // Begin acquiring images
        pCam->BeginAcquisition();
 
        cout << "[" << serialNumber << "] "
             << "Started acquiring images..." << endl;
 
        //
        // Retrieve, convert, and save images for each camera
        //
        const unsigned int k_numImages = 10;
 
        //
        // Create ImageProcessor instance for post processing images
        //
        ImageProcessor processor;
 
        //
        // Set default image processor color processing method
        //
        // *** NOTES ***
        // By default, if no specific color processing algorithm Is set, the image
        // processor will default to NEAREST_NEIGHBOR method.
        //
        processor.SetColorProcessing(SPINNAKER_COLOR_PROCESSING_ALGORITHM_HQ_LINEAR);
 
        cout << endl;
 
        for (unsigned int imageCnt = 0; imageCnt < k_numImages; imageCnt++)
        {
            try
            {
                // Retrieve next received image and ensure image completion
                ImagePtr pResultImage = pCam->GetNextImage(1000);
 
                if (pResultImage->IsIncomplete())
                {
                    cout << "[" << serialNumber << "] "
                         << "Image incomplete with image status " << pResultImage->GetImageStatus() << "..." << endl
                         << endl;
                }
                else
                {
                    // Convert image to mono 8
                    ImagePtr convertedImage = processor.Convert(pResultImage, PixelFormat_Mono8);
 
                    // Create a unique filename
                    std::ostringstream filename;
 
                    filename << "AcquisitionMultipleThread-";
                    if (serialNumber != "")
                    {
                        filename << serialNumber.c_str();
                    }
 
                    filename << "-" << imageCnt << ".jpg";
 
                    // Save image
                    convertedImage->Save(filename.str().c_str());
 
                    // Print image information
                    cout << "[" << serialNumber << "] "
                         << "Grabbed image " << imageCnt << ", width = " << pResultImage->GetWidth()
                         << ", height = " << pResultImage->GetHeight() << ". Image saved at " << filename.str() << endl;
                }
 
                // Release image
                pResultImage->Release();
 
                cout << endl;
            }
            catch (Spinnaker::Exception& e)
            {
                cout << "[" << serialNumber << "] "
                     << "Error: " << e.what() << endl;
            }
        }
 
        // End acquisition
        pCam->EndAcquisition();
            
#ifdef _DEBUG
        // Reset heartbeat for GEV camera
        ResetGVCPHeartbeat(pCam);
#endif
 
        // Deinitialize camera
        pCam->DeInit();
 
#if defined(_WIN32)
        return 1;
#else
        return (void*)1;
#endif
    }
    catch (Spinnaker::Exception& e)
    {
        cout << "Error: " << e.what() << endl;
#if defined(_WIN32)
        return 0;
#else
        return (void*)0;
#endif
    }
}
 
// This function acts as the body of the example
int RunMultipleCameras(CameraList camList)
{
    int result = 0;
    unsigned int camListSize = 0;
 
    try
    {
        // Retrieve camera list size
        camListSize = camList.GetSize();
 
        // Create an array of CameraPtrs. This array maintenances smart pointer's reference
        // count when CameraPtr is passed into grab thread as void pointer
 
        // Create an array of handles
        CameraPtr* pCamList = new CameraPtr[camListSize];
#if defined(_WIN32)
        HANDLE* grabThreads = new HANDLE[camListSize];
#else
        pthread_t* grabThreads = new pthread_t[camListSize];
#endif
 
        for (unsigned int i = 0; i < camListSize; i++)
        {
            // Select camera
            pCamList[i] = camList.GetByIndex(i);
            // Start grab thread
#if defined(_WIN32)
            grabThreads[i] = CreateThread(nullptr, 0, AcquireImages, &pCamList[i], 0, nullptr);
            assert(grabThreads[i] != nullptr);
#else
            int err = pthread_create(&(grabThreads[i]), nullptr, &AcquireImages, &pCamList[i]);
            assert(err == 0);
#endif
        }
 
#if defined(_WIN32)
        // Wait for all threads to finish
        WaitForMultipleObjects(
            camListSize, // number of threads to wait for
            grabThreads, // handles for threads to wait for
            TRUE,        // wait for all of the threads
            INFINITE     // wait forever
        );
 
        // Check thread return code for each camera
        for (unsigned int i = 0; i < camListSize; i++)
        {
            DWORD exitcode;
 
            BOOL rc = GetExitCodeThread(grabThreads[i], &exitcode);
            if (!rc)
            {
                cout << "Handle error from GetExitCodeThread() returned for camera at index " << i << endl;
                result = -1;
            }
            else if (!exitcode)
            {
                cout << "Grab thread for camera at index " << i
                     << " exited with errors."
                        "Please check onscreen print outs for error details"
                     << endl;
                result = -1;
            }
        }
 
#else
        for (unsigned int i = 0; i < camListSize; i++)
        {
            // Wait for all threads to finish
            void* exitcode;
            int rc = pthread_join(grabThreads[i], &exitcode);
            if (rc != 0)
            {
                cout << "Handle error from pthread_join returned for camera at index " << i << endl;
                result = -1;
            }
            else if ((int)(intptr_t)exitcode == 0) // check thread return code for each camera
            {
                cout << "Grab thread for camera at index " << i
                     << " exited with errors."
                        "Please check onscreen print outs for error details"
                     << endl;
                result = -1;
            }
        }
#endif
 
        // Clear CameraPtr array and close all handles
        for (unsigned int i = 0; i < camListSize; i++)
        {
            pCamList[i] = 0;
#if defined(_WIN32)
            CloseHandle(grabThreads[i]);
#endif
        }
 
        // Delete array pointer
        delete[] pCamList;
 
        // Delete array pointer
        delete[] grabThreads;
    }
    catch (Spinnaker::Exception& e)
    {
        cout << "Error: " << e.what() << endl;
        result = -1;
    }
 
    return result;
}
 
// Example entry point; please see Enumeration example for more in-depth
// comments on preparing and cleaning up the system.
int main(int /*argc*/, char** /*argv*/)
{
    // Since this application saves images in the current folder
    // we must ensure that we have permission to write to this folder.
    // If we do not have permission, fail right away.
    FILE* tempFile = fopen("test.txt", "w+");
    if (tempFile == nullptr)
    {
        cout << "Failed to create file in current folder.  Please check permissions." << endl;
        cout << "Press Enter to exit..." << endl;
        getchar();
        return -1;
    }
 
    fclose(tempFile);
    remove("test.txt");
 
    int result = 0;
 
    // Print application build information
    cout << "Application build date: " << __DATE__ << " " << __TIME__ << endl << endl;
 
    // Retrieve singleton reference to system object
    SystemPtr system = System::GetInstance();
 
    // Print out current library version
    const LibraryVersion spinnakerLibraryVersion = system->GetLibraryVersion();
    cout << "Spinnaker library version: " << spinnakerLibraryVersion.major << "." << spinnakerLibraryVersion.minor
         << "." << spinnakerLibraryVersion.type << "." << spinnakerLibraryVersion.build << endl
         << endl;
 
    // Retrieve list of cameras from the system
    CameraList camList = system->GetCameras();
 
    unsigned int numCameras = camList.GetSize();
 
    cout << "Number of cameras detected: " << numCameras << endl << endl;
 
    // Finish if there are no cameras
    if (numCameras == 0)
    {
        // Clear camera list before releasing system
        camList.Clear();
 
        // Release system
        system->ReleaseInstance();
 
        cout << "Not enough cameras!" << endl;
        cout << "Done! Press Enter to exit..." << endl;
        getchar();
 
        return -1;
    }
 
    // Run example on all cameras
    cout << endl << "Running example for all cameras..." << endl;
 
    result = RunMultipleCameras(camList);
 
    cout << "Example complete..." << endl << endl;
 
    // Clear camera list before releasing system
    camList.Clear();
 
    // Release system
    system->ReleaseInstance();
 
    cout << endl << "Done! Press Enter to exit..." << endl;
    getchar();
 
    return result;
}
