#
# Module manifest for module 'ParallelExecutionHelper'
#

@{

# Script module or binary module file associated with this manifest.
# RootModule = 'ParallelExecutionHelper.psd1'

# Version number of this module.
ModuleVersion = '1.0.0.1'
GUID = 'd3263ff0-ce0d-4280-9e57-6896a47cacf6'

Author = 'konstantin@kostov-bg.com'
CompanyName = 'www.kostov-bg.com'
Copyright = 'Copyright (c) 2018 Konstantin Kostov. All rights reserved.'

PowerShellVersion = '3.0'
CLRVersion = '4.0'

# Modules that must be imported into the global environment prior to importing this module
# RequiredModules = @()

# RequiredAssemblies = @()

NestedModules = @("ParallelExecutionHelper.dll")

# Functions to export from this module
#FunctionsToExport = ''

# Cmdlets to export from this module
CmdletsToExport = @('Invoke-ParallelProcessing')

}

