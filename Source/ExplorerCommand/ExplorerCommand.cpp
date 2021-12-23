#include "ExplorerCommand.h"
#include "Dll.h"
#include <ShlObj_core.h>
#include <Shlwapi.h>
#include <strsafe.h>
#include <new>
#include <wil/com.h>
#include <wil/resource.h>
#include <wil/result.h>
#include <wil/win32_helpers.h>

HRESULT ExplorerCommand::CreateInstance(REFIID riid, void** ppv)
{
    *ppv = nullptr;

    ExplorerCommand* pExplorerCommand = new(std::nothrow) ExplorerCommand();
    if (pExplorerCommand == nullptr)
    {
        return E_OUTOFMEMORY;
    }

    HRESULT hr = pExplorerCommand->QueryInterface(riid, ppv);
    pExplorerCommand->Release();
    return hr;
}

IFACEMETHODIMP ExplorerCommand::QueryInterface(REFIID riid, void** ppv)
{
    IUnknown* punk = nullptr;

    if (riid == __uuidof(IUnknown) || riid == __uuidof(IExplorerCommand))
    {
        punk = static_cast<IExplorerCommand*>(this);
    }

    *ppv = punk;

    if (punk == nullptr)
    {
        return E_NOINTERFACE;
    }

    punk->AddRef();
    return S_OK;
}

IFACEMETHODIMP_(ULONG) ExplorerCommand::AddRef()
{
    return InterlockedIncrement(&m_ref);
}

IFACEMETHODIMP_(ULONG) ExplorerCommand::Release()
{
    LONG cRef = InterlockedDecrement(&m_ref);
    if (cRef == 0)
    {
        delete this;
    }
    return cRef;
}

IFACEMETHODIMP ExplorerCommand::GetTitle(IShellItemArray* pItemArray, PWSTR* ppszName)
{
    wil::com_ptr_nothrow<IShellItem> item;
    RETURN_IF_FAILED(pItemArray->GetItemAt(0, &item));

    wil::unique_cotaskmem_string name;
    RETURN_IF_FAILED(item->GetDisplayName(SIGDN_PARENTRELATIVEPARSING, &name));

    PWSTR pszExt = PathFindExtensionW(name.get());
    if (wcscmp(pszExt, L".gltf") == 0)
    {
        RETURN_IF_FAILED(SHStrDup(L"Pack to Binary glTF", ppszName));
        m_pack = true;
    }
    else if (wcscmp(pszExt, L".glb") == 0)
    {
        RETURN_IF_FAILED(SHStrDup(L"Unpack to glTF", ppszName));
        m_pack = false;
    }
    else
    {
        return E_UNEXPECTED;
    }

    return S_OK;
}

IFACEMETHODIMP ExplorerCommand::GetIcon(IShellItemArray* pItemArray, PWSTR* ppszIcon)
{
    WCHAR szIcon[1024];
    DWORD result = GetModuleFileName(wil::GetModuleInstanceHandle(), szIcon, ARRAYSIZE(szIcon));
    RETURN_IF_WIN32_BOOL_FALSE(result < ARRAYSIZE(szIcon) ? TRUE : FALSE);
    RETURN_IF_FAILED(StringCchCat(szIcon, ARRAYSIZE(szIcon), L",0"));
    return SHStrDup(szIcon, ppszIcon);
}

IFACEMETHODIMP ExplorerCommand::GetToolTip(IShellItemArray* pItemArray, PWSTR* ppszInfotip)
{
    *ppszInfotip = nullptr;
    return E_NOTIMPL;
}

IFACEMETHODIMP ExplorerCommand::GetCanonicalName(GUID* pguidCommandName)
{
    *pguidCommandName = __uuidof(ExplorerCommand);
    return S_OK;
}

IFACEMETHODIMP ExplorerCommand::GetState(IShellItemArray* pItemArray, BOOL fOkToBeSlow, EXPCMDSTATE* pCmdState)
{
    *pCmdState = ECS_ENABLED;
    return S_OK;
}

IFACEMETHODIMP ExplorerCommand::Invoke(IShellItemArray* pItemArray, IBindCtx* pbc)
{
    struct Impl
    {
        static DWORD WINAPI ThreadProc(void* pv)
        {
            ExplorerCommand* pThis = static_cast<ExplorerCommand*>(pv);
            const DWORD ret = pThis->InvokeThreadProc();
            pThis->Release();
            return ret;
        }
    };

    HRESULT hr = CoMarshalInterThreadInterfaceInStream(__uuidof(pItemArray), pItemArray, &m_pstmShellItemArray);
    if (SUCCEEDED(hr))
    {
        AddRef();
        if (!SHCreateThread(Impl::ThreadProc, this, CTF_COINIT_STA | CTF_PROCESS_REF, nullptr))
        {
            Release();
        }
    }

    return S_OK;
}

IFACEMETHODIMP ExplorerCommand::GetFlags(EXPCMDFLAGS* pFlags)
{
    *pFlags = ECF_DEFAULT;
    return S_OK;
}

IFACEMETHODIMP ExplorerCommand::EnumSubCommands(IEnumExplorerCommand** ppEnum)
{
    *ppEnum = nullptr;
    return E_NOTIMPL;
}

HRESULT ExplorerCommand::InvokeCommand()
{
    wil::com_ptr_nothrow<IShellItemArray> itemArray;
    HRESULT hr = CoGetInterfaceAndReleaseStream(m_pstmShellItemArray, IID_PPV_ARGS(&itemArray));
    m_pstmShellItemArray = nullptr;
    RETURN_IF_FAILED(hr);

    wil::com_ptr_nothrow<IShellItem> item;
    RETURN_IF_FAILED(itemArray->GetItemAt(0, &item));

    WCHAR szExePath[MAX_PATH];
    DWORD result = GetModuleFileName(wil::GetModuleInstanceHandle(), szExePath, ARRAYSIZE(szExePath));
    RETURN_IF_WIN32_BOOL_FALSE(result < ARRAYSIZE(szExePath) ? TRUE : FALSE);
    *(PathFindFileName(szExePath) - 1) = L'\0';
    RETURN_IF_WIN32_BOOL_FALSE(PathAppend(szExePath, L"..\\glTF\\glTF.exe"));

    wil::unique_cotaskmem_string itemPath;
    RETURN_IF_FAILED(item->GetDisplayName(SIGDN_DESKTOPABSOLUTEPARSING, &itemPath));

    WCHAR szParameters[4096];
    RETURN_IF_FAILED(StringCchPrintf(szParameters, ARRAYSIZE(szParameters), L"%s \"%s\"", m_pack ? L"Pack" : L"Unpack", itemPath.get()));

    SHELLEXECUTEINFO info{};
    info.cbSize = sizeof(SHELLEXECUTEINFO);
    info.fMask = SEE_MASK_NOCLOSEPROCESS;
    info.lpFile = szExePath;
    info.lpParameters = szParameters;
    info.nShow = SW_SHOW;
    RETURN_IF_WIN32_BOOL_FALSE(ShellExecuteEx(&info));
    RETURN_IF_WIN32_BOOL_FALSE(WaitForSingleObject(info.hProcess, INFINITE) != WAIT_FAILED ? TRUE : FALSE);
    RETURN_IF_WIN32_BOOL_FALSE(CloseHandle(info.hProcess));

    if (m_pack)
    {
        wil::com_ptr_nothrow<IShellItem> parent;
        RETURN_IF_FAILED(item->GetParent(&parent));

        wil::unique_cotaskmem_string itemRelativePath;
        RETURN_IF_FAILED(item->GetDisplayName(SIGDN_PARENTRELATIVEPARSING, &itemRelativePath));

        WCHAR szItemRelativePath[MAX_PATH];
        RETURN_IF_FAILED(StringCchCopy(szItemRelativePath, ARRAYSIZE(szItemRelativePath), itemRelativePath.get()));

        PWSTR pszExtension = PathFindExtension(szItemRelativePath);
        pszExtension[3] = L'b';
        pszExtension[4] = L'\0';

        wil::com_ptr_nothrow<IShellItem> newItem;
        RETURN_IF_FAILED(SHCreateItemFromRelativeName(parent.get(), szItemRelativePath, nullptr, IID_PPV_ARGS(&newItem)));

        wil::com_ptr_nothrow<IParentAndItem> parentAndItem;
        RETURN_IF_FAILED(newItem.query_to(&parentAndItem));

        PIDLIST_ABSOLUTE pidlParent;
        PITEMID_CHILD pidlChild;
        RETURN_IF_FAILED(parentAndItem->GetParentAndItem(&pidlParent, nullptr, &pidlChild));

        PCITEMID_CHILD pidlChildren[] = { pidlChild };
        RETURN_IF_FAILED(SHOpenFolderAndSelectItems(pidlParent, ARRAYSIZE(pidlChildren), pidlChildren, OFASI_EDIT));
    }

    return S_OK;
}

DWORD ExplorerCommand::InvokeThreadProc()
{
    HRESULT hr = InvokeCommand();
    if (FAILED(hr))
    {
        WCHAR szMessage[4096];
        if (FAILED(StringCchPrintf(szMessage, ARRAYSIZE(szMessage), L"An error occurred.\n0x%08x", hr)))
        {
            StringCchCopy(szMessage, ARRAYSIZE(szMessage), L"An unknown error occurred.");
        }

        MessageBox(nullptr, szMessage, L"glTF Shell Extensions", MB_OK);
    }

    return 0;
}
