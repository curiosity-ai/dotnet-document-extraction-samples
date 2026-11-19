targetScope = 'resourceGroup'

@description('Prefix for resource names.')
param environmentName string

@description('Region. Content Understanding and Mistral OCR are both available in eastus2, swedencentral, and westus3.')
@allowed(['eastus2', 'swedencentral', 'westus3'])
param location string = 'eastus2'

@description('Object ID of the person or identity that runs the samples. azd fills this in for you.')
param principalId string

@description('User for a signed-in person, ServicePrincipal for CI.')
param principalType string = 'User'

var token = toLower(uniqueString(subscription().id, resourceGroup().id, environmentName))
var accountName = 'docx-${token}'
var mistralModel = 'mistral-ocr-4-0'

// One Foundry (AIServices) resource serves Document Intelligence, Content Understanding, and Mistral OCR.
// Local auth is off, so the samples use Microsoft Entra ID through DefaultAzureCredential. No keys.
resource account 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: accountName
  location: location
  kind: 'AIServices'
  sku: { name: 'S0' }
  identity: { type: 'SystemAssigned' }
  properties: {
    customSubDomainName: accountName
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
    allowProjectManagement: true
  }
  tags: { 'azd-env-name': environmentName }
}

resource mistral 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: account
  name: mistralModel
  sku: { name: 'GlobalStandard', capacity: 1 }
  properties: {
    model: { format: 'Mistral AI', name: mistralModel, version: '1' }
  }
}

// Cognitive Services User covers Document Intelligence and Content Understanding.
// Azure AI User covers model inference, including Mistral OCR.
var roles = [
  'a97b65f3-24c7-4388-baec-2e87135dc908' // Cognitive Services User
  '53ca6127-db72-4b80-b1b0-d745d6d5456d' // Foundry User (formerly Azure AI User)
]

resource assignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for role in roles: {
  scope: account
  name: guid(account.id, principalId, role)
  properties: {
    principalId: principalId
    principalType: principalType
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', role)
  }
}]

output AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT string = 'https://${accountName}.cognitiveservices.azure.com/'
output AZURE_CONTENT_UNDERSTANDING_ENDPOINT string = 'https://${accountName}.services.ai.azure.com/'
output AZURE_MISTRAL_OCR_URI string = 'https://${accountName}.services.ai.azure.com/providers/mistral/azure/ocr'
output AZURE_MISTRAL_OCR_MODEL string = mistral.name
