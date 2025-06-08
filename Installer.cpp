#define _CRT_SECURE_NO_WARNINGS
#include <windows.h>
#include <iostream>
#include <string>
#include <shlobj.h>
#include <shlwapi.h>
#include <comdef.h>
#include <mshtml.h>
#include <atlbase.h>
#include <atlcom.h>

#pragma comment(lib, "shlwapi.lib")

bool CreateShortcut(const std::wstring& shortcutPath, const std::wstring& targetPath) {
    HRESULT hr;
    IShellLink* pShellLink = nullptr;
    IPersistFile* pPersistFile = nullptr;


    hr = CoCreateInstance(CLSID_ShellLink, NULL, CLSCTX_INPROC_SERVER,
        IID_IShellLink, (LPVOID*)&pShellLink);

    if (SUCCEEDED(hr)) { 

        pShellLink->SetPath(targetPath.c_str());
        

        wchar_t workingDir[MAX_PATH];
        wcscpy_s(workingDir, targetPath.c_str());
        PathRemoveFileSpecW(workingDir);
        pShellLink->SetWorkingDirectory(workingDir);


        hr = pShellLink->QueryInterface(IID_IPersistFile, (LPVOID*)&pPersistFile);
        if (SUCCEEDED(hr)) {
            hr = pPersistFile->Save(shortcutPath.c_str(), TRUE);
            pPersistFile->Release();
        }
        pShellLink->Release();
    }

    return SUCCEEDED(hr);
}

bool CopyDirectory(const std::wstring& sourceDir, const std::wstring& destDir) {
    WIN32_FIND_DATAW findData;
    HANDLE hFind = FindFirstFileW((sourceDir + L"\\*").c_str(), &findData);
    
    if (hFind == INVALID_HANDLE_VALUE) {
        return false;
    }

    do {
        if (wcscmp(findData.cFileName, L".") == 0 || wcscmp(findData.cFileName, L"..") == 0) {
            continue;
        }

        std::wstring sourcePath = sourceDir + L"\\" + findData.cFileName;
        std::wstring destPath = destDir + L"\\" + findData.cFileName;

        if (findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
            CreateDirectoryW(destPath.c_str(), NULL);
            CopyDirectory(sourcePath, destPath);
        } else {
            CopyFileW(sourcePath.c_str(), destPath.c_str(), FALSE);
        }
    } while (FindNextFileW(hFind, &findData));

    FindClose(hFind);
    return true;
}

int main() {

    SetConsoleOutputCP(CP_UTF8);
    setvbuf(stdout, nullptr, _IONBF, 0);

    try {
        std::cout << "=== DBD Launcher Installer ===" << std::endl;
        std::cout << "Начинаем установку...\n" << std::endl;


        HRESULT hr = CoInitialize(NULL);
        if (FAILED(hr)) {
            throw std::runtime_error("Ошибка инициализации COM");
        }


        std::wstring installPath = L"C:\\OLD DBD SERVER";


        if (!CreateDirectoryW(installPath.c_str(), NULL) && GetLastError() != ERROR_ALREADY_EXISTS) {
            throw std::runtime_error("Не удалось создать папку установки");
        }

        wchar_t currentDir[MAX_PATH];
        GetModuleFileNameW(NULL, currentDir, MAX_PATH);
        PathRemoveFileSpecW(currentDir);

        std::cout << "Копирование файлов..." << std::endl;
        if (!CopyDirectory(currentDir, installPath)) {
            throw std::runtime_error("Ошибка при копировании файлов");
        }

 
        std::cout << "\nСоздание ярлыка на рабочем столе..." << std::endl;
        wchar_t desktopPath[MAX_PATH];
        if (SUCCEEDED(SHGetFolderPathW(NULL, CSIDL_DESKTOP, NULL, 0, desktopPath))) {
            std::wstring shortcutPath = std::wstring(desktopPath) + L"\\DBD Launcher.lnk";
            std::wstring targetPath = installPath + L"\\LauncherDBD.exe";

            if (CreateShortcut(shortcutPath, targetPath)) {
                std::cout << "Ярлык успешно создан" << std::endl;
            } else {
                std::cout << "Ошибка при создании ярлыка" << std::endl;
            }
        }

        std::cout << "\nУстановка завершена успешно!" << std::endl;
        std::wcout << L"Лаунчер установлен в: " << installPath << std::endl;
        std::cout << "Ярлык создан на рабочем столе." << std::endl;
        std::cout << "\nНажмите любую клавишу для выхода...";
        std::cin.get();

        CoUninitialize();
        return 0;
    }
    catch (const std::exception& e) {
        std::cout << "\nОшибка при установке: " << e.what() << std::endl;
        std::cout << "\nНажмите любую клавишу для выхода...";
        std::cin.get();
        return 1;
    }
} 